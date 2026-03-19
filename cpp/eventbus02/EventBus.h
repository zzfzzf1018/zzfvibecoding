#pragma once

#include <cstddef>
#include <cstdint>
#include <condition_variable>
#include <functional>
#include <memory>
#include <mutex>
#include <queue>
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

class EventBus {
public:
    // Construct an independent bus instance (non-singleton).
    EventBus() : nextToken_(1), workerRunning_(false), stopRequested_(false) {}

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

    // Set callback used to marshal work onto UI thread.
    // The provided executor should enqueue and run tasks on the UI/main thread.
    void SetUiExecutor(const std::function<void(std::function<void()>)>& executor) {
        std::lock_guard<std::mutex> guard(mutex_);
        uiExecutor_ = executor;
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
    // Handlers are copied to a snapshot first to avoid holding locks during callbacks.
    template <typename Event>
    void Publish(const Event& event) const {
        std::vector<typename EventContainer<Event>::HandlerEntry> snapshot;
        std::function<void(std::function<void()>)> uiExecutor;
        {
            std::lock_guard<std::mutex> guard(mutex_);
            const std::unordered_map<std::type_index, std::unique_ptr<IEventContainer> >::const_iterator it = containers_.find(std::type_index(typeid(Event)));
            if (it == containers_.end()) {
                return;
            }

            const EventContainer<Event>* container = static_cast<const EventContainer<Event>*>(it->second.get());
            snapshot = container->Snapshot();
            uiExecutor = uiExecutor_;
        }

        typename std::vector<typename EventContainer<Event>::HandlerEntry>::const_iterator it = snapshot.begin();
        for (; it != snapshot.end(); ++it) {
            if (it->target == DispatchTarget::UiThread && uiExecutor) {
                const Event copied = event;
                const std::function<void(const Event&)> callback = it->callback;
                uiExecutor([callback, copied]() {
                    callback(copied);
                });
                continue;
            }

            it->callback(event);
        }
    }

    // Queue an event for asynchronous delivery by the background worker.
    // Returns false when worker is not running.
    template <typename Event>
    bool PublishAsync(const Event& event) {
        {
            std::lock_guard<std::mutex> queueGuard(queueMutex_);
            if (!workerRunning_) {
                return false;
            }

            const Event copied = event;
            queue_.push([this, copied]() {
                this->Publish<Event>(copied);
            });
        }

        queueCv_.notify_one();
        return true;
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
        std::lock_guard<std::mutex> guard(queueMutex_);
        if (workerRunning_) {
            return true;
        }

        stopRequested_ = false;
        workerRunning_ = true;
        worker_ = std::thread(&EventBus::WorkerLoop, this);
        return true;
    }

    // Stop worker thread and clear remaining queued tasks.
    void StopAsyncWorker() {
        {
            std::lock_guard<std::mutex> guard(queueMutex_);
            if (!workerRunning_) {
                return;
            }
            stopRequested_ = true;
        }

        queueCv_.notify_all();
        if (worker_.joinable()) {
            worker_.join();
        }

        std::lock_guard<std::mutex> guard(queueMutex_);
        workerRunning_ = false;
        stopRequested_ = false;
        std::queue<std::function<void()> > empty;
        queue_.swap(empty);
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
                    return stopRequested_ || !queue_.empty();
                });

                if (stopRequested_ && queue_.empty()) {
                    break;
                }

                task = queue_.front();
                queue_.pop();
            }

            task();
        }
    }

private:
    mutable std::mutex mutex_;
    std::unordered_map<std::type_index, std::unique_ptr<IEventContainer> > containers_;
    std::unordered_map<std::uint64_t, std::type_index> tokenIndex_;
    std::function<void(std::function<void()>)> uiExecutor_;
    std::uint64_t nextToken_;

    std::mutex queueMutex_;
    std::condition_variable queueCv_;
    std::queue<std::function<void()> > queue_;
    std::thread worker_;
    bool workerRunning_;
    bool stopRequested_;
};

}  // namespace eb
