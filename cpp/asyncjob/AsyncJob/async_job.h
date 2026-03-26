#pragma once

#include <atomic>
#include <chrono>
#include <condition_variable>
#include <functional>
#include <future>
#include <memory>
#include <mutex>
#include <optional>
#include <queue>
#include <stdexcept>
#include <thread>
#include <type_traits>
#include <utility>
#include <vector>

class ThreadPool {
public:
    explicit ThreadPool(size_t threads = std::thread::hardware_concurrency())
        : stop_(false) {
        if (threads == 0) {
            threads = 1;
        }
        workers_.reserve(threads);
        for (size_t i = 0; i < threads; ++i) {
            workers_.emplace_back([this]() {
                worker_loop_();
            });
        }
    }

    ~ThreadPool() {
        {
            std::lock_guard<std::mutex> lock(mutex_);
            stop_ = true;
        }
        condition_.notify_all();
        for (std::thread& worker : workers_) {
            if (worker.joinable()) {
                worker.join();
            }
        }
    }

    ThreadPool(const ThreadPool&) = delete;
    ThreadPool& operator=(const ThreadPool&) = delete;

    template <typename F>
    auto submit(F&& task) -> std::future<std::invoke_result_t<F>> {
        using Result = std::invoke_result_t<F>;
        auto packaged = std::make_shared<std::packaged_task<Result()>>(std::forward<F>(task));
        std::future<Result> future = packaged->get_future();
        {
            std::lock_guard<std::mutex> lock(mutex_);
            if (stop_) {
                throw std::runtime_error("thread pool stopped");
            }
            tasks_.emplace([packaged]() {
                (*packaged)();
            });
        }
        condition_.notify_one();
        return future;
    }

private:
    void worker_loop_() {
        while (true) {
            std::function<void()> task;
            {
                std::unique_lock<std::mutex> lock(mutex_);
                condition_.wait(lock, [this]() {
                    return stop_ || !tasks_.empty();
                });
                if (stop_ && tasks_.empty()) {
                    return;
                }
                task = std::move(tasks_.front());
                tasks_.pop();
            }
            task();
        }
    }

    std::vector<std::thread> workers_;
    std::queue<std::function<void()>> tasks_;
    std::mutex mutex_;
    std::condition_variable condition_;
    bool stop_;
};

enum class JobStatus {
    Idle,
    Running,
    Completed,
    Cancelled,
    Failed
};

enum class TimeoutPolicy {
    KeepRunning,
    Cancel
};

class CancellationToken {
public:
    bool is_cancelled() const {
        return flag_ && flag_->load();
    }

private:
    template <typename R>
    friend class AsyncJob;

    explicit CancellationToken(std::shared_ptr<std::atomic<bool>> flag)
        : flag_(std::move(flag)) {}

    std::shared_ptr<std::atomic<bool>> flag_;
};

template <typename R>
class AsyncJob {
public:
    using Executor = std::function<void(std::function<void()>)>;
    using Task = std::function<R(const CancellationToken&)>;
    using Callback = std::function<void(const R&)>;
    using ErrorCallback = std::function<void(std::exception_ptr)>;

    explicit AsyncJob(Task task)
        : task_(std::move(task)),
          cancel_flag_(std::make_shared<std::atomic<bool>>(false)) {
        if (!task_) {
            throw std::invalid_argument("task is empty");
        }
    }

    explicit AsyncJob(std::function<R()> task)
        : simple_task_(std::move(task)),
          cancel_flag_(std::make_shared<std::atomic<bool>>(false)) {
        if (!simple_task_) {
            throw std::invalid_argument("task is empty");
        }
    }

    void set_callback(Callback callback) {
        callback_ = std::move(callback);
    }

    void set_error_callback(ErrorCallback callback) {
        error_callback_ = std::move(callback);
    }

    void set_executor(Executor executor) {
        executor_ = std::move(executor);
    }

    void set_callback_executor(Executor executor) {
        callback_executor_ = std::move(executor);
    }

    void use_thread_pool(bool enable) {
        use_thread_pool_ = enable;
    }

    bool start() {
        std::lock_guard<std::mutex> lock(mutex_);
        if (running_) {
            return false;
        }

        cancel_flag_->store(false);
        set_status_(JobStatus::Running);
        exception_ = nullptr;

        CancellationToken token(cancel_flag_);
        auto work = [this, token]() -> R {
            try {
                R result = task_ ? task_(token) : simple_task_();
                invoke_callback_(result);
                finalize_status_(token);
                running_.store(false);
                return result;
            } catch (...) {
                handle_error_(std::current_exception());
                running_.store(false);
                throw;
            }
        };

        std::future<R> future = submit_(std::move(work));
        future_ = future.share();
        running_.store(true);
        return true;
    }

    bool restart() {
        cancel();
        wait();
        reset();
        return start();
    }

    void cancel() {
        cancel_flag_->store(true);
    }

    bool is_cancelled() const {
        return cancel_flag_->load();
    }

    JobStatus status() const {
        return status_.load();
    }

    void wait() {
        std::shared_future<R> future = get_future_snapshot_();
        if (future.valid()) {
            future.wait();
        }
    }

    template <typename Rep, typename Period>
    bool wait_for(const std::chrono::duration<Rep, Period>& timeout,
        TimeoutPolicy policy = TimeoutPolicy::KeepRunning) {
        std::shared_future<R> future = get_future_snapshot_();
        if (!future.valid()) {
            return false;
        }
        if (future.wait_for(timeout) == std::future_status::ready) {
            return true;
        }
        if (policy == TimeoutPolicy::Cancel) {
            cancel();
        }
        return false;
    }

    template <typename Rep, typename Period>
    bool wait_for_interruptible(const std::chrono::duration<Rep, Period>& timeout,
        const std::chrono::milliseconds& poll = std::chrono::milliseconds(50)) {
        auto start = std::chrono::steady_clock::now();
        while (!is_cancelled()) {
            auto elapsed = std::chrono::steady_clock::now() - start;
            if (elapsed >= timeout) {
                break;
            }
            auto remaining = std::chrono::duration_cast<std::chrono::milliseconds>(timeout - elapsed);
            auto slice = remaining < poll ? remaining : poll;
            if (wait_for(slice, TimeoutPolicy::KeepRunning)) {
                return true;
            }
        }
        return false;
    }

    R get() {
        std::shared_future<R> future = get_future_snapshot_();
        if (!future.valid()) {
            throw std::runtime_error("job not started");
        }
        return future.get();
    }

    template <typename Rep, typename Period>
    std::optional<R> get_for(const std::chrono::duration<Rep, Period>& timeout,
        TimeoutPolicy policy = TimeoutPolicy::Cancel) {
        std::shared_future<R> future = get_future_snapshot_();
        if (!future.valid()) {
            return std::nullopt;
        }
        if (future.wait_for(timeout) != std::future_status::ready) {
            if (policy == TimeoutPolicy::Cancel) {
                cancel();
            }
            return std::nullopt;
        }
        return future.get();
    }

    template <typename Rep, typename Period>
    std::optional<R> get_for_or_cancel(const std::chrono::duration<Rep, Period>& timeout) {
        return get_for(timeout, TimeoutPolicy::Cancel);
    }

    std::shared_future<R> get_future() const {
        std::shared_future<R> future = get_future_snapshot_();
        if (!future.valid()) {
            throw std::runtime_error("job not started");
        }
        return future;
    }

    std::exception_ptr get_exception() const {
        std::lock_guard<std::mutex> lock(mutex_);
        return exception_;
    }

    void reset() {
        std::lock_guard<std::mutex> lock(mutex_);
        future_ = std::shared_future<R>();
        exception_ = nullptr;
        cancel_flag_->store(false);
        set_status_(JobStatus::Idle);
    }

private:
    Task task_;
    std::function<R()> simple_task_;
    Callback callback_;
    ErrorCallback error_callback_;
    Executor executor_;
    Executor callback_executor_;
    std::shared_ptr<std::atomic<bool>> cancel_flag_;
    std::shared_future<R> future_;
    std::atomic<bool> running_{ false };
    std::atomic<JobStatus> status_{ JobStatus::Idle };
    std::exception_ptr exception_;
    bool use_thread_pool_{ false };
    mutable std::mutex mutex_;

    std::shared_future<R> get_future_snapshot_() const {
        std::lock_guard<std::mutex> lock(mutex_);
        return future_;
    }

    void set_status_(JobStatus status) {
        status_.store(status);
    }

    void finalize_status_(const CancellationToken& token) {
        set_status_(token.is_cancelled() ? JobStatus::Cancelled : JobStatus::Completed);
    }

    void handle_error_(std::exception_ptr exception) {
        {
            std::lock_guard<std::mutex> lock(mutex_);
            exception_ = exception;
        }
        set_status_(JobStatus::Failed);
        if (error_callback_) {
            if (callback_executor_) {
                callback_executor_([callback = error_callback_, exception]() {
                    callback(exception);
                });
            } else {
                error_callback_(exception);
            }
        }
    }

    void invoke_callback_(const R& result) {
        if (!callback_) {
            return;
        }
        if (callback_executor_) {
            auto result_copy = std::make_shared<R>(result);
            callback_executor_([callback = callback_, result_copy]() {
                callback(*result_copy);
            });
            return;
        }
        callback_(result);
    }

    template <typename Work>
    std::future<R> submit_(Work&& work) {
        if (executor_) {
            auto packaged = std::make_shared<std::packaged_task<R()>>(std::forward<Work>(work));
            std::future<R> future = packaged->get_future();
            executor_([packaged]() {
                (*packaged)();
            });
            return future;
        }
        if (use_thread_pool_) {
            return default_pool_().submit(std::forward<Work>(work));
        }
        return std::async(std::launch::async, std::forward<Work>(work));
    }

    static ThreadPool& default_pool_() {
        static ThreadPool pool;
        return pool;
    }
};

template <>
class AsyncJob<void> {
public:
    using Executor = std::function<void(std::function<void()>)>;
    using Task = std::function<void(const CancellationToken&)>;
    using Callback = std::function<void()>;
    using ErrorCallback = std::function<void(std::exception_ptr)>;

    explicit AsyncJob(Task task)
        : task_(std::move(task)),
          cancel_flag_(std::make_shared<std::atomic<bool>>(false)) {
        if (!task_) {
            throw std::invalid_argument("task is empty");
        }
    }

    explicit AsyncJob(std::function<void()> task)
        : simple_task_(std::move(task)),
          cancel_flag_(std::make_shared<std::atomic<bool>>(false)) {
        if (!simple_task_) {
            throw std::invalid_argument("task is empty");
        }
    }

    void set_callback(Callback callback) {
        callback_ = std::move(callback);
    }

    void set_error_callback(ErrorCallback callback) {
        error_callback_ = std::move(callback);
    }

    void set_executor(Executor executor) {
        executor_ = std::move(executor);
    }

    void set_callback_executor(Executor executor) {
        callback_executor_ = std::move(executor);
    }

    void use_thread_pool(bool enable) {
        use_thread_pool_ = enable;
    }

    bool start() {
        std::lock_guard<std::mutex> lock(mutex_);
        if (running_) {
            return false;
        }

        cancel_flag_->store(false);
        set_status_(JobStatus::Running);
        exception_ = nullptr;

        CancellationToken token(cancel_flag_);
        auto work = [this, token]() {
            try {
                task_ ? task_(token) : simple_task_();
                invoke_callback_();
                finalize_status_(token);
                running_.store(false);
            } catch (...) {
                handle_error_(std::current_exception());
                running_.store(false);
                throw;
            }
        };

        std::future<void> future = submit_(std::move(work));
        future_ = future.share();
        running_.store(true);
        return true;
    }

    bool restart() {
        cancel();
        wait();
        reset();
        return start();
    }

    void cancel() {
        cancel_flag_->store(true);
    }

    bool is_cancelled() const {
        return cancel_flag_->load();
    }

    JobStatus status() const {
        return status_.load();
    }

    void wait() {
        std::shared_future<void> future = get_future_snapshot_();
        if (future.valid()) {
            future.wait();
        }
    }

    template <typename Rep, typename Period>
    bool wait_for(const std::chrono::duration<Rep, Period>& timeout,
        TimeoutPolicy policy = TimeoutPolicy::KeepRunning) {
        std::shared_future<void> future = get_future_snapshot_();
        if (!future.valid()) {
            return false;
        }
        if (future.wait_for(timeout) == std::future_status::ready) {
            return true;
        }
        if (policy == TimeoutPolicy::Cancel) {
            cancel();
        }
        return false;
    }

    template <typename Rep, typename Period>
    bool wait_for_interruptible(const std::chrono::duration<Rep, Period>& timeout,
        const std::chrono::milliseconds& poll = std::chrono::milliseconds(50)) {
        auto start = std::chrono::steady_clock::now();
        while (!is_cancelled()) {
            auto elapsed = std::chrono::steady_clock::now() - start;
            if (elapsed >= timeout) {
                break;
            }
            auto remaining = std::chrono::duration_cast<std::chrono::milliseconds>(timeout - elapsed);
            auto slice = remaining < poll ? remaining : poll;
            if (wait_for(slice, TimeoutPolicy::KeepRunning)) {
                return true;
            }
        }
        return false;
    }

    void get() {
        std::shared_future<void> future = get_future_snapshot_();
        if (!future.valid()) {
            throw std::runtime_error("job not started");
        }
        future.get();
    }

    template <typename Rep, typename Period>
    bool wait_for_or_cancel(const std::chrono::duration<Rep, Period>& timeout) {
        return wait_for(timeout, TimeoutPolicy::Cancel);
    }

    std::shared_future<void> get_future() const {
        std::shared_future<void> future = get_future_snapshot_();
        if (!future.valid()) {
            throw std::runtime_error("job not started");
        }
        return future;
    }

    std::exception_ptr get_exception() const {
        std::lock_guard<std::mutex> lock(mutex_);
        return exception_;
    }

    void reset() {
        std::lock_guard<std::mutex> lock(mutex_);
        future_ = std::shared_future<void>();
        exception_ = nullptr;
        cancel_flag_->store(false);
        set_status_(JobStatus::Idle);
    }

private:
    Task task_;
    std::function<void()> simple_task_;
    Callback callback_;
    ErrorCallback error_callback_;
    Executor executor_;
    Executor callback_executor_;
    std::shared_ptr<std::atomic<bool>> cancel_flag_;
    std::shared_future<void> future_;
    std::atomic<bool> running_{ false };
    std::atomic<JobStatus> status_{ JobStatus::Idle };
    std::exception_ptr exception_;
    bool use_thread_pool_{ false };
    mutable std::mutex mutex_;

    std::shared_future<void> get_future_snapshot_() const {
        std::lock_guard<std::mutex> lock(mutex_);
        return future_;
    }

    void set_status_(JobStatus status) {
        status_.store(status);
    }

    void finalize_status_(const CancellationToken& token) {
        set_status_(token.is_cancelled() ? JobStatus::Cancelled : JobStatus::Completed);
    }

    void handle_error_(std::exception_ptr exception) {
        {
            std::lock_guard<std::mutex> lock(mutex_);
            exception_ = exception;
        }
        set_status_(JobStatus::Failed);
        if (error_callback_) {
            if (callback_executor_) {
                callback_executor_([callback = error_callback_, exception]() {
                    callback(exception);
                });
            } else {
                error_callback_(exception);
            }
        }
    }

    void invoke_callback_() {
        if (!callback_) {
            return;
        }
        if (callback_executor_) {
            callback_executor_([callback = callback_]() {
                callback();
            });
            return;
        }
        callback_();
    }

    template <typename Work>
    std::future<void> submit_(Work&& work) {
        if (executor_) {
            auto packaged = std::make_shared<std::packaged_task<void()>>(std::forward<Work>(work));
            std::future<void> future = packaged->get_future();
            executor_([packaged]() {
                (*packaged)();
            });
            return future;
        }
        if (use_thread_pool_) {
            return default_pool_().submit(std::forward<Work>(work));
        }
        return std::async(std::launch::async, std::forward<Work>(work));
    }

    static ThreadPool& default_pool_() {
        static ThreadPool pool;
        return pool;
    }
};
