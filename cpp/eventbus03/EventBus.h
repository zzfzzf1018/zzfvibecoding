#pragma once

#include <cstddef>
#include <cstdint>
#include <chrono>
#include <condition_variable>
#include <functional>
#include <memory>
#include <mutex>
#include <queue>
#include <string>
#include <thread>
#include <typeindex>
#include <typeinfo>
#include <unordered_map>
#include <utility>
#include <vector>

namespace eb {

enum class RegistrationRule {
    OneToOne,
    OneToMany
};

enum class RegistrationRelation {
    None,
    OneToOne,
    OneToMany
};

enum class DispatchTarget {
    CurrentThread,
    UiThread
};

enum class AsyncQueuePolicy {
    RejectNew,
    WaitForSpace,
    DropOldest
};

enum class PublishStatus {
    Ok,
    NoSubscribers,
    UiExecutorNotConfigured,
    WorkerNotRunning,
    Stopping,
    QueueFull,
    TimedOut,
    CallbackException
};

class EventBus {
public:
    struct AsyncRuntimeStats {
        std::size_t pendingTaskCount;
        std::size_t droppedTaskCount;
        bool workerRunning;
        bool stopInProgress;
    };

    // Construct an independent bus instance (non-singleton).
    EventBus()
        : nextToken_(1),
          maxQueueSize_(1024),
          queuePolicy_(AsyncQueuePolicy::RejectNew),
          droppedTaskCount_(0),
                    acceptingPublishes_(true),
          workerRunning_(false),
                    stopRequested_(false),
                    stopInProgress_(false) {}

    // Ensure worker thread is stopped before the bus is destroyed.
    ~EventBus() {
        StopAsyncWorker();
    }

    EventBus(const EventBus&) = delete;
    EventBus& operator=(const EventBus&) = delete;

    EventBus(EventBus&&) = delete;
    EventBus& operator=(EventBus&&) = delete;

    // Register a free function/lambda callback for an event type.
    // Returns a non-zero token on success, or 0 when registration is rejected.
    template <typename Event, typename Func>
    std::uint64_t Subscribe(
        Func&& func,
        RegistrationRule rule = RegistrationRule::OneToMany,
        DispatchTarget target = DispatchTarget::CurrentThread) {
        std::lock_guard<std::mutex> guard(mutex_);
        EventContainer<Event>* container = GetOrCreateContainer<Event>();
        if (!container->CanAccept(rule)) {
            return 0;
        }

        const std::uint64_t token = nextToken_++;
        container->AddHandler(token, std::function<void(const Event&)>(std::forward<Func>(func)), target);
        tokenIndex_.emplace(token, std::type_index(typeid(Event)));
        return token;
    }

    // Register an object member function callback for an event type.
    // This overload wraps member function invocation into a lambda callback.
    template <typename Event, typename Obj>
    std::uint64_t Subscribe(
        Obj* obj,
        void (Obj::*method)(const Event&),
        RegistrationRule rule = RegistrationRule::OneToMany,
        DispatchTarget target = DispatchTarget::CurrentThread) {
        return Subscribe<Event>(
            [obj, method](const Event& e) {
                (obj->*method)(e);
            },
            rule,
            target);
    }

    // Register a member callback guarded by weak_ptr to avoid calling destroyed objects.
    template <typename Event, typename Obj>
    std::uint64_t SubscribeWeak(
        const std::weak_ptr<Obj>& weakObj,
        void (Obj::*method)(const Event&),
        RegistrationRule rule = RegistrationRule::OneToMany,
        DispatchTarget target = DispatchTarget::CurrentThread) {
        return Subscribe<Event>(
            [weakObj, method](const Event& e) {
                const std::shared_ptr<Obj> strong = weakObj.lock();
                if (strong) {
                    (strong.get()->*method)(e);
                }
            },
            rule,
            target);
    }

    // Set callback used to marshal work onto UI thread.
    // The provided executor should enqueue and run tasks on the UI/main thread.
    void SetUiExecutor(const std::function<void(std::function<void()>)>& executor) {
        std::lock_guard<std::mutex> guard(mutex_);
        uiExecutor_ = executor;
    }

    // Configure bounded async queue behavior.
    void ConfigureAsyncQueue(std::size_t maxQueueSize, AsyncQueuePolicy policy) {
        {
            std::lock_guard<std::mutex> guard(queueMutex_);
            maxQueueSize_ = maxQueueSize == 0 ? 1 : maxQueueSize;
            queuePolicy_ = policy;
        }
        // Wake waiters so they can re-evaluate policy/size changes immediately.
        queueCv_.notify_all();
    }

    // Return count of async tasks dropped due to queue policy.
    std::size_t DroppedAsyncTaskCount() const {
        std::lock_guard<std::mutex> guard(queueMutex_);
        return droppedTaskCount_;
    }

    // Return current number of pending async tasks.
    std::size_t PendingAsyncTaskCount() const {
        std::lock_guard<std::mutex> guard(queueMutex_);
        return PendingTaskCountUnsafe();
    }

    // Return whether the async worker thread is currently running.
    bool IsAsyncWorkerRunning() const {
        std::lock_guard<std::mutex> guard(queueMutex_);
        return workerRunning_;
    }

    // Return a consistent snapshot of async runtime stats.
    AsyncRuntimeStats GetAsyncRuntimeStats() const {
        std::lock_guard<std::mutex> guard(queueMutex_);
        AsyncRuntimeStats stats;
        stats.pendingTaskCount = PendingTaskCountUnsafe();
        stats.droppedTaskCount = droppedTaskCount_;
        stats.workerRunning = workerRunning_;
        stats.stopInProgress = stopInProgress_;
        return stats;
    }

    // Reset async queue statistics counters.
    void ResetAsyncStats() {
        std::lock_guard<std::mutex> guard(queueMutex_);
        droppedTaskCount_ = 0;
    }

    // Remove an existing subscription by token.
    // Returns true if a handler was removed.
    bool Unsubscribe(std::uint64_t token) {
        std::lock_guard<std::mutex> guard(mutex_);
        const std::unordered_map<std::uint64_t, std::type_index>::iterator idxIt = tokenIndex_.find(token);
        if (idxIt == tokenIndex_.end()) {
            return false;
        }

        const std::type_index type = idxIt->second;
        std::unordered_map<std::type_index, std::unique_ptr<IEventContainer> >::iterator it = containers_.find(type);
        if (it == containers_.end()) {
            tokenIndex_.erase(idxIt);
            return false;
        }

        const bool erased = it->second->Remove(token);
        tokenIndex_.erase(idxIt);

        if (it->second->Empty()) {
            containers_.erase(it);
        }

        return erased;
    }

    // Publish an event synchronously in the caller thread.
    // Returns status code so callers can react to configuration/runtime failures.
    template <typename Event>
    PublishStatus Publish(const Event& event) const {
        std::vector<typename EventContainer<Event>::HandlerEntry> snapshot;
        std::function<void(std::function<void()>)> uiExecutor;
        {
            std::lock_guard<std::mutex> guard(mutex_);
            const std::unordered_map<std::type_index, std::unique_ptr<IEventContainer> >::const_iterator it = containers_.find(std::type_index(typeid(Event)));
            if (it == containers_.end()) {
                return PublishStatus::NoSubscribers;
            }

            const EventContainer<Event>* container = static_cast<const EventContainer<Event>*>(it->second.get());
            snapshot = container->Snapshot();
            uiExecutor = uiExecutor_;

            if (snapshot.empty()) {
                return PublishStatus::NoSubscribers;
            }

            if (container->HasUiTarget() && !uiExecutor) {
                return PublishStatus::UiExecutorNotConfigured;
            }
        }

        typename std::vector<typename EventContainer<Event>::HandlerEntry>::const_iterator it = snapshot.begin();
        for (; it != snapshot.end(); ++it) {
            try {
                if (it->target == DispatchTarget::UiThread) {
                    const Event copied = event;
                    const std::function<void(const Event&)> callback = it->callback;
                    uiExecutor([callback, copied]() {
                        callback(copied);
                    });
                    continue;
                }

                it->callback(event);
            } catch (...) {
                return PublishStatus::CallbackException;
            }
        }

        return PublishStatus::Ok;
    }

    // Explicit synchronous publish entry.
    // This is an alias of Publish(...) for readability in business code.
    template <typename Event>
    PublishStatus PublishSync(const Event& event) const {
        return Publish<Event>(event);
    }

    // Queue an event for asynchronous delivery by the background worker.
    // Returns status code for immediate queueing/configuration result.
    template <typename Event>
    PublishStatus PublishAsync(const Event& event) {
        {
            std::lock_guard<std::mutex> guard(mutex_);
            const std::unordered_map<std::type_index, std::unique_ptr<IEventContainer> >::const_iterator it = containers_.find(std::type_index(typeid(Event)));
            if (it == containers_.end()) {
                return PublishStatus::NoSubscribers;
            }

            const EventContainer<Event>* container = static_cast<const EventContainer<Event>*>(it->second.get());
            if (container->Size() == 0) {
                return PublishStatus::NoSubscribers;
            }
            if (container->HasUiTarget() && !uiExecutor_) {
                return PublishStatus::UiExecutorNotConfigured;
            }
        }

        {
            std::unique_lock<std::mutex> queueGuard(queueMutex_);
            if (!workerRunning_) {
                return PublishStatus::WorkerNotRunning;
            }
            if (!acceptingPublishes_) {
                return PublishStatus::Stopping;
            }

            while (PendingTaskCountUnsafe() >= maxQueueSize_) {
                if (queuePolicy_ == AsyncQueuePolicy::RejectNew) {
                    return PublishStatus::QueueFull;
                }
                if (queuePolicy_ == AsyncQueuePolicy::WaitForSpace) {
                    queueCv_.wait(queueGuard, [this]() {
                        return stopRequested_ || !workerRunning_ || !acceptingPublishes_ || PendingTaskCountUnsafe() < maxQueueSize_;
                    });

                    if (stopRequested_ || !workerRunning_) {
                        return PublishStatus::WorkerNotRunning;
                    }
                    if (!acceptingPublishes_) {
                        return PublishStatus::Stopping;
                    }
                    continue;
                }
                DropOldestPendingTaskUnsafe();
            }

            const Event copied = event;
            queue_.push([this, copied]() {
                this->Publish<Event>(copied);
            });
        }

        queueCv_.notify_one();
        return PublishStatus::Ok;
    }

    // Queue an event for asynchronous delivery and bound waiting time when queue policy is WaitForSpace.
    // For non-waiting policies, this behaves the same as PublishAsync.
    template <typename Event>
    PublishStatus PublishAsyncWaitForTimeout(const Event& event, std::uint32_t timeoutMs) {
        AsyncQueuePolicy policy;
        {
            std::lock_guard<std::mutex> guard(queueMutex_);
            policy = queuePolicy_;
        }

        if (policy != AsyncQueuePolicy::WaitForSpace) {
            return PublishAsync<Event>(event);
        }

        {
            std::lock_guard<std::mutex> guard(mutex_);
            const std::unordered_map<std::type_index, std::unique_ptr<IEventContainer> >::const_iterator it = containers_.find(std::type_index(typeid(Event)));
            if (it == containers_.end()) {
                return PublishStatus::NoSubscribers;
            }

            const EventContainer<Event>* container = static_cast<const EventContainer<Event>*>(it->second.get());
            if (container->Size() == 0) {
                return PublishStatus::NoSubscribers;
            }
            if (container->HasUiTarget() && !uiExecutor_) {
                return PublishStatus::UiExecutorNotConfigured;
            }
        }

        const std::chrono::steady_clock::time_point deadline =
            std::chrono::steady_clock::now() + std::chrono::milliseconds(timeoutMs);

        {
            std::unique_lock<std::mutex> queueGuard(queueMutex_);
            if (!workerRunning_) {
                return PublishStatus::WorkerNotRunning;
            }
            if (!acceptingPublishes_) {
                return PublishStatus::Stopping;
            }

            while (PendingTaskCountUnsafe() >= maxQueueSize_) {
                const bool ready = queueCv_.wait_until(queueGuard, deadline, [this]() {
                    return stopRequested_ || !workerRunning_ || !acceptingPublishes_ || PendingTaskCountUnsafe() < maxQueueSize_;
                });

                if (!ready) {
                    return PublishStatus::TimedOut;
                }
                if (stopRequested_ || !workerRunning_) {
                    return PublishStatus::WorkerNotRunning;
                }
                if (!acceptingPublishes_) {
                    return PublishStatus::Stopping;
                }
            }

            const Event copied = event;
            queue_.push([this, copied]() {
                this->Publish<Event>(copied);
            });
        }

        queueCv_.notify_one();
        return PublishStatus::Ok;
    }

    // Queue an event for asynchronous delivery with key-based coalescing.
    // A pending task with the same key is replaced by the latest payload.
    template <typename Event>
    PublishStatus PublishAsyncCoalesced(const Event& event, const std::string& key) {
        if (key.empty()) {
            return PublishAsync<Event>(event);
        }

        {
            std::lock_guard<std::mutex> guard(mutex_);
            const std::unordered_map<std::type_index, std::unique_ptr<IEventContainer> >::const_iterator it = containers_.find(std::type_index(typeid(Event)));
            if (it == containers_.end()) {
                return PublishStatus::NoSubscribers;
            }

            const EventContainer<Event>* container = static_cast<const EventContainer<Event>*>(it->second.get());
            if (container->Size() == 0) {
                return PublishStatus::NoSubscribers;
            }
            if (container->HasUiTarget() && !uiExecutor_) {
                return PublishStatus::UiExecutorNotConfigured;
            }
        }

        {
            std::unique_lock<std::mutex> queueGuard(queueMutex_);
            if (!workerRunning_) {
                return PublishStatus::WorkerNotRunning;
            }
            if (!acceptingPublishes_) {
                return PublishStatus::Stopping;
            }

            const Event copied = event;
            std::unordered_map<std::string, std::function<void()> >::iterator existing = coalescedTasks_.find(key);
            if (existing != coalescedTasks_.end()) {
                existing->second = [this, copied]() {
                    this->Publish<Event>(copied);
                };
                return PublishStatus::Ok;
            }

            while (PendingTaskCountUnsafe() >= maxQueueSize_) {
                if (queuePolicy_ == AsyncQueuePolicy::RejectNew) {
                    return PublishStatus::QueueFull;
                }
                if (queuePolicy_ == AsyncQueuePolicy::WaitForSpace) {
                    queueCv_.wait(queueGuard, [this]() {
                        return stopRequested_ || !workerRunning_ || !acceptingPublishes_ || PendingTaskCountUnsafe() < maxQueueSize_;
                    });

                    if (stopRequested_ || !workerRunning_) {
                        return PublishStatus::WorkerNotRunning;
                    }
                    if (!acceptingPublishes_) {
                        return PublishStatus::Stopping;
                    }
                    continue;
                }
                DropOldestPendingTaskUnsafe();
            }

            coalescedTasks_[key] = [this, copied]() {
                this->Publish<Event>(copied);
            };
            coalescedOrder_.push(key);
        }

        queueCv_.notify_one();
        return PublishStatus::Ok;
    }

    // Get current number of subscribers for a specific event type.
    template <typename Event>
    std::size_t SubscriberCount() const {
        std::lock_guard<std::mutex> guard(mutex_);
        const std::unordered_map<std::type_index, std::unique_ptr<IEventContainer> >::const_iterator it = containers_.find(std::type_index(typeid(Event)));
        if (it == containers_.end()) {
            return 0;
        }

        return static_cast<const EventContainer<Event>*>(it->second.get())->Size();
    }

    // Detect current relation mode for an event type (none/one-to-one/one-to-many).
    template <typename Event>
    RegistrationRelation DetectRelation() const {
        std::lock_guard<std::mutex> guard(mutex_);
        const std::unordered_map<std::type_index, std::unique_ptr<IEventContainer> >::const_iterator it = containers_.find(std::type_index(typeid(Event)));
        if (it == containers_.end()) {
            return RegistrationRelation::None;
        }

        const EventContainer<Event>* container = static_cast<const EventContainer<Event>*>(it->second.get());
        return container->Relation();
    }

    // Start the single async worker thread. Safe to call multiple times.
    bool StartAsyncWorker() {
        std::thread staleWorker;
        {
            std::unique_lock<std::mutex> lock(queueMutex_);
            if (workerRunning_ && !stopInProgress_) {
                return true;
            }

            if (stopInProgress_) {
                workerExitCv_.wait(lock, [this]() {
                    return !stopInProgress_;
                });
            }

            if (workerRunning_) {
                return true;
            }

            if (worker_.joinable()) {
                staleWorker = std::move(worker_);
            }

            acceptingPublishes_ = true;
            stopRequested_ = false;
        }

        if (staleWorker.joinable()) {
            staleWorker.join();
        }

        std::lock_guard<std::mutex> guard(queueMutex_);
        if (workerRunning_) {
            return true;
        }

        try {
            worker_ = std::thread(&EventBus::WorkerLoop, this);
            workerRunning_ = true;
            stopInProgress_ = false;
            return true;
        } catch (...) {
            workerRunning_ = false;
            acceptingPublishes_ = false;
            stopRequested_ = false;
            stopInProgress_ = false;
            return false;
        }
    }

    // Stop worker thread and clear remaining queued tasks.
    void StopAsyncWorker() {
        std::thread workerToJoin;
        bool selfStop = false;
        bool waitForExit = false;
        {
            std::unique_lock<std::mutex> lock(queueMutex_);
            if (!workerRunning_) {
                if (!stopInProgress_) {
                    if (worker_.joinable()) {
                        workerToJoin = std::move(worker_);
                    }
                    if (!workerToJoin.joinable()) {
                        return;
                    }
                } else {
                    waitForExit = true;
                }
            } else if (stopInProgress_) {
                waitForExit = true;
            } else {
                stopInProgress_ = true;
                acceptingPublishes_ = false;
                stopRequested_ = true;

                if (worker_.joinable() && worker_.get_id() == std::this_thread::get_id()) {
                    selfStop = true;
                } else if (worker_.joinable()) {
                    workerToJoin = std::move(worker_);
                } else {
                    waitForExit = true;
                }
            }
        }

        if (!waitForExit) {
            queueCv_.notify_all();
        }

        if (selfStop) {
            std::lock_guard<std::mutex> guard(queueMutex_);
            if (worker_.joinable() && worker_.get_id() == std::this_thread::get_id()) {
                // Self-stop cannot join current thread; detach and let worker loop finalize state on exit.
                worker_.detach();
            }
            return;
        }

        if (workerToJoin.joinable()) {
            workerToJoin.join();
        }

        std::unique_lock<std::mutex> lock(queueMutex_);
        workerExitCv_.wait(lock, [this]() {
            return !workerRunning_ && !stopInProgress_;
        });
    }

private:
    struct IEventContainer {
        // Polymorphic base for type-erased event containers.
        virtual ~IEventContainer() {}

        // Remove a handler token from this container.
        virtual bool Remove(std::uint64_t token) = 0;

        // Report whether this container has no handlers.
        virtual bool Empty() const = 0;
    };

    template <typename Event>
    class EventContainer : public IEventContainer {
    public:
        struct HandlerEntry {
            std::function<void(const Event&)> callback;
            DispatchTarget target;
        };

        // Container starts uninitialized and adopts rule on first successful registration.
        EventContainer() : initialized_(false), rule_(RegistrationRule::OneToMany) {}

        // Validate whether a new registration can be accepted with the requested rule.
        bool CanAccept(RegistrationRule newRule) {
            if (!initialized_) {
                initialized_ = true;
                rule_ = newRule;
                return true;
            }

            if (rule_ != newRule) {
                return false;
            }

            if (rule_ == RegistrationRule::OneToOne && !handlers_.empty()) {
                return false;
            }

            return true;
        }

        // Insert or replace a callback bound to a token.
        void AddHandler(
            std::uint64_t token,
            const std::function<void(const Event&)>& handler,
            DispatchTarget target) {
            HandlerEntry entry;
            entry.callback = handler;
            entry.target = target;
            handlers_[token] = entry;
        }

        // Remove callback by token.
        virtual bool Remove(std::uint64_t token) {
            return handlers_.erase(token) > 0;
        }

        // Check whether this event type currently has no handlers.
        virtual bool Empty() const {
            return handlers_.empty();
        }

        // Build a callback snapshot for lock-free invocation outside container lock.
        std::vector<HandlerEntry> Snapshot() const {
            std::vector<HandlerEntry> snapshot;
            snapshot.reserve(handlers_.size());
            typename std::unordered_map<std::uint64_t, HandlerEntry>::const_iterator it = handlers_.begin();
            for (; it != handlers_.end(); ++it) {
                snapshot.push_back(it->second);
            }
            return snapshot;
        }

        // Check whether at least one callback requires UiThread dispatch.
        bool HasUiTarget() const {
            typename std::unordered_map<std::uint64_t, HandlerEntry>::const_iterator it = handlers_.begin();
            for (; it != handlers_.end(); ++it) {
                if (it->second.target == DispatchTarget::UiThread) {
                    return true;
                }
            }
            return false;
        }

        // Return current number of handlers.
        std::size_t Size() const {
            return handlers_.size();
        }

        // Return active relation classification for this event type.
        RegistrationRelation Relation() const {
            if (!initialized_ || handlers_.empty()) {
                return RegistrationRelation::None;
            }
            return rule_ == RegistrationRule::OneToOne ? RegistrationRelation::OneToOne : RegistrationRelation::OneToMany;
        }

    private:
        bool initialized_;
        RegistrationRule rule_;
        std::unordered_map<std::uint64_t, HandlerEntry> handlers_;
    };

    // Get existing container for event type, or create one on first use.
    template <typename Event>
    EventContainer<Event>* GetOrCreateContainer() {
        const std::type_index key(typeid(Event));
        std::unordered_map<std::type_index, std::unique_ptr<IEventContainer> >::iterator it = containers_.find(key);
        if (it == containers_.end()) {
            std::unique_ptr<IEventContainer> ptr(new EventContainer<Event>());
            containers_.emplace(key, std::move(ptr));
            it = containers_.find(key);
        }

        return static_cast<EventContainer<Event>*>(it->second.get());
    }

    // Worker thread loop: wait for tasks and execute until stop is requested.
    void WorkerLoop() {
        for (;;) {
            std::function<void()> task;
            {
                std::unique_lock<std::mutex> lock(queueMutex_);
                queueCv_.wait(lock, [this]() {
                    return stopRequested_ || !queue_.empty() || !coalescedOrder_.empty();
                });

                if (stopRequested_ && queue_.empty() && coalescedOrder_.empty()) {
                    break;
                }

                if (!queue_.empty()) {
                    task = queue_.front();
                    queue_.pop();
                    queueCv_.notify_one();
                } else {
                    const std::string key = coalescedOrder_.front();
                    coalescedOrder_.pop();

                    std::unordered_map<std::string, std::function<void()> >::iterator it = coalescedTasks_.find(key);
                    if (it != coalescedTasks_.end()) {
                        task = it->second;
                        coalescedTasks_.erase(it);
                        queueCv_.notify_one();
                    }
                }
            }

            if (task) {
                try {
                    task();
                } catch (...) {
                    // Keep worker alive even if a queued task throws.
                }
            }
        }

        std::lock_guard<std::mutex> guard(queueMutex_);
        workerRunning_ = false;
        acceptingPublishes_ = false;
        stopRequested_ = false;
        stopInProgress_ = false;
        std::queue<std::function<void()> > empty;
        queue_.swap(empty);
        std::unordered_map<std::string, std::function<void()> > emptyCoalesced;
        coalescedTasks_.swap(emptyCoalesced);
        std::queue<std::string> emptyKeys;
        coalescedOrder_.swap(emptyKeys);
        workerExitCv_.notify_all();
    }

    std::size_t PendingTaskCountUnsafe() const {
        return queue_.size() + coalescedTasks_.size();
    }

    void DropOldestPendingTaskUnsafe() {
        if (!queue_.empty()) {
            queue_.pop();
            ++droppedTaskCount_;
            return;
        }

        if (!coalescedOrder_.empty()) {
            const std::string key = coalescedOrder_.front();
            coalescedOrder_.pop();
            std::unordered_map<std::string, std::function<void()> >::iterator it = coalescedTasks_.find(key);
            if (it != coalescedTasks_.end()) {
                coalescedTasks_.erase(it);
                ++droppedTaskCount_;
            }
        }
    }

private:
    mutable std::mutex mutex_;
    std::unordered_map<std::type_index, std::unique_ptr<IEventContainer> > containers_;
    std::unordered_map<std::uint64_t, std::type_index> tokenIndex_;
    std::function<void(std::function<void()>)> uiExecutor_;
    std::uint64_t nextToken_;

    mutable std::mutex queueMutex_;
    std::condition_variable queueCv_;
    std::condition_variable workerExitCv_;
    std::queue<std::function<void()> > queue_;
    std::unordered_map<std::string, std::function<void()> > coalescedTasks_;
    std::queue<std::string> coalescedOrder_;
    std::size_t maxQueueSize_;
    AsyncQueuePolicy queuePolicy_;
    std::size_t droppedTaskCount_;
    bool acceptingPublishes_;
    std::thread worker_;
    bool workerRunning_;
    bool stopRequested_;
    bool stopInProgress_;
};

}  // namespace eb
