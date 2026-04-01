#include <ppltasks.h>
#include <iostream>
#include <thread>
#include <chrono>
#include <sstream>
#include <functional>

// 使用微软并发库命名空间
using namespace concurrency;
using namespace std::chrono_literals;

// 辅助函数：打印带线程ID的日志
void Log(const char* msg) {
    std::stringstream ss;
    ss << "[Thread " << std::this_thread::get_id() << "] " << msg << "\n";
    std::cout << ss.str();
}

// 模拟 C# Task.Delay (支持取消)
// 返回一个 task<void>，在指定毫秒后完成
task<void> AsyncDelay(int ms, cancellation_token token) {
    // create_task 可以把一个 Lambda 转为 Task
    return create_task([ms, token]() {
        // 这里模拟耗时操作，分段睡眠以便及时响应取消
        const int slice_ms = 50;
        int remaining = ms;
        while (remaining > 0) {
            if (token.is_canceled()) {
                throw task_canceled();
            }
            const int step = remaining < slice_ms ? remaining : slice_ms;
            std::this_thread::sleep_for(std::chrono::milliseconds(step));
            remaining -= step;
        }
    }, token);
}

// -----------------------------------------------------------------------------
// 模拟 Async Method
// -----------------------------------------------------------------------------
// 这种写法对应 C# 的:
// Task MyAsyncMethod() {
//    print("Start");
//    await Task.Delay(1000);
//    print("After 1s");
//    await Task.Delay(500);
//    print("Done");
// }
// -----------------------------------------------------------------------------
task<void> MyAsyncMethod(cancellation_token token) {
    Log("Start MyAsyncMethod (Sync part)");

    // 无法使用 'co_await'，必须使用 '.then()' 链式调用
    // 这就是 async/await 出现之前的写法 (CPS 变换)
    
    return AsyncDelay(1000, token).then([token]() {
        // 第一个 await 后的代码
        Log("Resumed MyAsyncMethod after 1s delay");
        
        // 启动第二个异步任务
        return AsyncDelay(500, token);

    }).then([]() {
        // 第二个 await 后的代码
        Log("Resumed MyAsyncMethod after 0.5s delay");
        Log("Async method finished.");
    });
}

// -----------------------------------------------------------------------------
// 通用异步任务封装
// -----------------------------------------------------------------------------
template <typename T>
class AsyncJob {
public:
    using JobFunc = std::function<task<T>(cancellation_token)>;
    using TimeoutCallback = std::function<void()>;
    using WorkFunc = std::function<T(cancellation_token)>;

    AsyncJob() : token_source_(), timeout_token_source_(), task_(), has_task_(false), has_timeout_task_(false) {}

    void Start(JobFunc job) {
        CancelAndWait();
        token_source_ = cancellation_token_source();
        task_ = job(token_source_.get_token());
        has_task_ = true;
        timeout_token_source_.cancel();
        has_timeout_task_ = false;
    }

    void StartFromCallable(WorkFunc work) {
        Start([work](cancellation_token token) {
            return create_task([work, token]() {
                return work(token);
            }, token);
        });
    }

    void StartWithTimeout(JobFunc job, int timeout_ms, TimeoutCallback on_timeout = nullptr) {
        Start(job);
        auto timeout_source = token_source_;
        timeout_token_source_ = cancellation_token_source();
        auto timeout_token = timeout_token_source_.get_token();
        timeout_task_ = create_task([timeout_ms, timeout_source, on_timeout, timeout_token]() {
            const int slice_ms = 50;
            int remaining = timeout_ms;
            while (remaining > 0) {
                if (timeout_token.is_canceled()) {
                    return;
                }
                const int step = remaining < slice_ms ? remaining : slice_ms;
                std::this_thread::sleep_for(std::chrono::milliseconds(step));
                remaining -= step;
            }
            if (on_timeout) {
                on_timeout();
            }
            timeout_source.cancel();
        }, timeout_token);
        has_timeout_task_ = true;
    }

    void Cancel() {
        if (has_task_) {
            token_source_.cancel();
        }
        timeout_token_source_.cancel();
    }

    T Wait() {
        if (!has_task_) {
            return T();
        }
        return task_.get();
    }

    void CancelAndWait() {
        Cancel();
        SafeWait();
    }

    ~AsyncJob() {
        CancelAndWait();
    }

private:
    T SafeWait() {
        if (!has_task_) {
            return T();
        }
        try {
            task_.wait();
            if (has_timeout_task_) {
                timeout_task_.wait();
            }
        }
        catch (const task_canceled&) {
            // 取消是预期行为
        }
        catch (const std::exception& e) {
            std::cout << "Exception: " << e.what() << std::endl;
        }
        return T();
    }

    cancellation_token_source token_source_;
    cancellation_token_source timeout_token_source_;
    task<T> task_;
    bool has_task_;
    task<void> timeout_task_;
    bool has_timeout_task_;
};

// void 特化
template <>
class AsyncJob<void> {
public:
    using JobFunc = std::function<task<void>(cancellation_token)>;
    using TimeoutCallback = std::function<void()>;
    using WorkFunc = std::function<void(cancellation_token)>;

    AsyncJob() : token_source_(), timeout_token_source_(), task_(), has_task_(false), has_timeout_task_(false) {}

    void Start(JobFunc job) {
        CancelAndWait();
        token_source_ = cancellation_token_source();
        task_ = job(token_source_.get_token());
        has_task_ = true;
        timeout_token_source_.cancel();
        has_timeout_task_ = false;
    }

    void StartFromCallable(WorkFunc work) {
        Start([work](cancellation_token token) {
            return create_task([work, token]() {
                work(token);
            }, token);
        });
    }

    void StartWithTimeout(JobFunc job, int timeout_ms, TimeoutCallback on_timeout = nullptr) {
        Start(job);
        auto timeout_source = token_source_;
        timeout_token_source_ = cancellation_token_source();
        auto timeout_token = timeout_token_source_.get_token();
        timeout_task_ = create_task([timeout_ms, timeout_source, on_timeout, timeout_token]() {
            const int slice_ms = 50;
            int remaining = timeout_ms;
            while (remaining > 0) {
                if (timeout_token.is_canceled()) {
                    return;
                }
                const int step = remaining < slice_ms ? remaining : slice_ms;
                std::this_thread::sleep_for(std::chrono::milliseconds(step));
                remaining -= step;
            }
            if (on_timeout) {
                on_timeout();
            }
            timeout_source.cancel();
        }, timeout_token);
        has_timeout_task_ = true;
    }

    void Cancel() {
        if (has_task_) {
            token_source_.cancel();
        }
        timeout_token_source_.cancel();
    }

    void Wait() {
        if (!has_task_) {
            return;
        }
        task_.get();
    }

    void CancelAndWait() {
        Cancel();
        SafeWait();
    }

    ~AsyncJob() {
        CancelAndWait();
    }

private:
    void SafeWait() {
        if (!has_task_) {
            return;
        }
        try {
            task_.wait();
            if (has_timeout_task_) {
                timeout_task_.wait();
            }
        }
        catch (const task_canceled&) {
            // 取消是预期行为
        }
        catch (const std::exception& e) {
            std::cout << "Exception: " << e.what() << std::endl;
        }
    }

    cancellation_token_source token_source_;
    cancellation_token_source timeout_token_source_;
    task<void> task_;
    bool has_task_;
    task<void> timeout_task_;
    bool has_timeout_task_;
};

// 带返回值的示例任务
task<int> ComputeValueAsync(cancellation_token token) {
    return AsyncDelay(300, token).then([]() {
        return 42;
    });
}

int main() {
    Log("Main started");

    // 启动异步任务 (由对象托管生命周期)
    AsyncJob<void> job;
    job.StartWithTimeout([](cancellation_token token) {
        return MyAsyncMethod(token);
    }, 1200, []() {
        Log("MyAsyncMethod timeout");
    });

    // 启动带返回值的异步任务
    AsyncJob<int> job2;
    job2.StartWithTimeout([](cancellation_token token) {
        return ComputeValueAsync(token);
    }, 1000, []() {
        Log("ComputeValueAsync timeout");
    });

    Log("Back in Main after calling async method");

    // 模拟主线程做其他事情
    std::this_thread::sleep_for(200ms);

    // 等待带返回值任务完成并读取结果 (取消会抛异常)
    try {
        int value = job2.Wait();
        std::cout << "Computed value: " << value << std::endl;
    }
    catch (const task_canceled&) {
        std::cout << "ComputeValueAsync canceled" << std::endl;
    }

    // 等待 void 任务完成 (取消会抛异常)
    try {
        job.Wait();
    }
    catch (const task_canceled&) {
        std::cout << "MyAsyncMethod canceled" << std::endl;
    }

    Log("Main finished");

    return 0;
}
