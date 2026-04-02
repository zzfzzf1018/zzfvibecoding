# cpp_ui_sub_interact

一个最小可运行的 Win32 示例工程，用于演示下面这类线程协作框架：

- 子线程执行耗时业务逻辑
- 业务完成后安全地通知主 UI 线程
- 所有 UI 更新都在主线程中完成

## 方案说明

核心思路：

1. 子线程不直接操作 UI。
2. 子线程把一个待执行任务投递到 `UiTaskDispatcher` 的线程安全队列。
3. `UiTaskDispatcher` 通过 `PostMessage` 向主窗口发送自定义消息。
4. 主线程在窗口消息循环中收到该消息后，批量取出任务并执行，从而安全更新 UI。

这样做的优点：

- 符合 Win32 UI 线程模型
- 可扩展为多个业务线程共用一个 UI 分发器
- 业务代码和 UI 更新逻辑解耦

## 主要文件

- `src/UiTaskDispatcher.h`：UI 线程任务分发器
- `src/MainWindow.h`：示例主窗口类
- `src/MainWindow.cpp`：窗口创建、线程启动、UI 更新逻辑
- `src/main.cpp`：程序入口

## 编译环境

- Visual Studio 2017 或更高版本
- 平台工具集：`v141`
- 字符集：Unicode

## 运行效果

程序启动后点击按钮，子线程会模拟执行 5 个业务步骤，并在每一步结束后通知主线程更新界面文本。

## 可复用方式

如果你要接入自己的工程，重点复用 `UiTaskDispatcher` 即可：

1. 在 UI 主窗口初始化时调用 `Initialize(hwnd, messageId)`。
2. 子线程通过 `PostTask(...)` 投递一个 UI 更新任务。
3. 主窗口在对应自定义消息里调用 `DispatchPendingTasks()`。
4. 窗口销毁前调用 `Shutdown()`，避免销毁后继续投递。