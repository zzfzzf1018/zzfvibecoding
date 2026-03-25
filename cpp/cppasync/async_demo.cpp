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

// 模拟 C# Task.Delay
// 返回一个 task<void>，在指定毫秒后完成
task<void> AsyncDelay(int ms) {
    // create_task 可以把一个 Lambda 转为 Task
    return create_task([ms]() {
        // 这里模拟耗时操作，实际上 PPL 内部会用到线程池
        std::this_thread::sleep_for(std::chrono::milliseconds(ms));
    });
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
task<void> MyAsyncMethod() {
    Log("Start MyAsyncMethod (Sync part)");

    // 无法使用 'co_await'，必须使用 '.then()' 链式调用
    // 这就是 async/await 出现之前的写法 (CPS 变换)
    return AsyncDelay(1000).then([]() {
        // 第一个 await 后的代码
        Log("Resumed MyAsyncMethod after 1s delay");
        
        // 启动第二个异步任务
        return AsyncDelay(500);

    }).then([]() {
        // 第二个 await 后的代码
        Log("Resumed MyAsyncMethod after 0.5s delay");
        Log("Async method finished.");
    });
}

int main() {
    Log("Main started");

    // 启动异步任务
    task<void> t = MyAsyncMethod();

    Log("Back in Main after calling async method");

    // PPL 的 task 是非阻塞的，这里等待任务完成
    // 仅为了演示，防止主程序直接退出
    try {
        // 在 C# 中通常是 await MyAsyncMethod() 或者 task.Wait()
        t.wait();
    }
    catch (const std::exception& e) {
        std::cout << "Exception: " << e.what() << std::endl;
    }

    Log("Main finished");

    return 0;
}
