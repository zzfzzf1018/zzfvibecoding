#include <ppltasks.h>
#include <iostream>
#include <thread>
#include <chrono>
#include <sstream>

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
// 任务封装：析构时自动取消并等待，避免主线程退出后任务仍在运行
// -----------------------------------------------------------------------------
class AsyncJob {
public:
    AsyncJob() : token_source_(), task_() {}

    void Start() {
        if (task_.is_done()) {
            token_source_ = cancellation_token_source();
        }
        task_ = MyAsyncMethod(token_source_.get_token());
    }

    void CancelAndWait() {
        token_source_.cancel();
        SafeWait();
    }

    ~AsyncJob() {
        CancelAndWait();
    }

private:
    void SafeWait() {
        if (!task_.is_done()) {
            try {
                task_.wait();
            }
            catch (const task_canceled&) {
                // 取消是预期行为
            }
            catch (const std::exception& e) {
                std::cout << "Exception: " << e.what() << std::endl;
            }
        }
    }

    cancellation_token_source token_source_;
    task<void> task_;
};

int main() {
    Log("Main started");

    // 启动异步任务 (由对象托管生命周期)
    AsyncJob job;
    job.Start();

    Log("Back in Main after calling async method");

    // 模拟主线程做其他事情
    std::this_thread::sleep_for(200ms);

    Log("Main finished");

    return 0;
}
