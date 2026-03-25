# C++ Async Await 示例 (MSVC V141 / VS 2017)

这是一个展示如何在 C++ 中实现类似于 C# `async/await` 机制的示例代码。

该示例使用了 Microsoft Visual Studio 2017 (MSVC V141) 引入的 C++ 协程 (Coroutines TS) 特性。

## 前置要求

- Visual Studio 2017 (或更高版本，但在较新版本中某些语法可能已变更为标准 C++20)
- Windows 环境

## 编译方法

打开 **Developer Command Prompt for VS 2017** (或者 VS 2019/2022)，进入代码目录，运行以下命令：

```cmd
cl /EHsc /await /std:c++14 async_demo.cpp
```

* `/EHsc`: 启用 C++ 异常处理
* `/await`: 启用协程支持 (这是 VS 2017/2019 必需的)
* `/std:c++14`: 指定语言标准 (VS 2017 默认，也可选 /std:c++17)

## 运行

编译成功后，将在当前目录生成 `async_demo.exe`。直接运行即可：

```cmd
async_demo.exe
```

## 关键点说明

1.  **`co_await`**: 等同于 C# 的 `await`。它会挂起当前函数的执行，并将控制权交还给调用者，直到等待的操作完成。
2.  **`promise_type`**: C++ 协程的核心。它定义了如何创建协程对象 (`Task`)，以及协程开始 (`initial_suspend`) 和结束 (`final_suspend`) 时的行为。
3.  **`Task`**: 我们自定义的类，类似于 C# 的 `Task` 或 JavaScript 的 `Promise`，用于管理协程的生命周期。
4.  **`await_suspend`**: 当 `co_await` 发生时，此方法被调用。在这个例子中，我们在这里启动了一个后台线程来模拟异步 I/O。

## 注意事项

* 这是一个简化的示例，旨在演示机制。生产级代码需要处理更复杂的生命周期管理（防止悬挂引用）、线程调度（Context Switching）和异常传播。
* 在标准 C++20 中，协程已正式标准化，但在 VS 2017 (V141) 中使用的是技术规范 (TS) 版本，命名空间为 `std::experimental`。
