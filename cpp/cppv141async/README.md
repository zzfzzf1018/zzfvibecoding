# cppv141async

一个面向 MSVC v141 的最小示例，演示如何在 MFC 中做出接近 C# async/await 的使用体验：

- UI 事件处理函数中使用 `co_await`
- 同步业务函数不改签名、不改实现
- 业务逻辑自动切到后台线程执行
- 执行完成后自动切回 UI 线程更新控件

## 方案要点

本示例基于 MSVC `/await` 和 `std::experimental::coroutine`。

核心能力在 `MfcAsyncAwait/AsyncAwait.h` 中：

- `cppv141async::resume_background()`：切到线程池线程
- `cppv141async::resume_ui()`：切回 MFC UI 线程
- `cppv141async::background_invoke(...)`：把任意同步调用放到后台执行，并在返回前切回 UI 线程
- `CPPV141_ASYNC(expr)`：把原来的同步调用表达式直接包装成可 `co_await` 的异步调用

典型调用方式：

```cpp
auto text = co_await CPPV141_ASYNC(m_service.QuerySlowReport(requestId));
m_resultEdit.SetWindowTextW(text);
```

这里 `m_service.QuerySlowReport(requestId)` 仍然是同步函数，不需要改成回调、future、promise 或额外的异步版本。

## 示例效果

点击对话框中的 `Load Data` 按钮后：

1. 主线程立刻更新状态文本为 `Loading from worker thread...`
2. `BizService::QuerySlowReport` 在线程池线程中执行
3. 完成后自动回到 UI 线程
4. 主线程更新 `EDIT` 控件内容与状态文本

## 构建要求

- Visual Studio 2017 或更高版本
- 安装 MFC 组件
- 使用 `v141` 工具集
- 项目已打开 `/await`

工程文件已经包含这些设置：

- `PlatformToolset = v141`
- `UseOfMfc = Dynamic`
- `LanguageStandard = stdcpp17`
- `AdditionalOptions = /await`

## 文件说明

- `MfcAsyncAwait/AsyncAwait.h`：协程任务类型、线程切换 awaiter、辅助宏
- `MfcAsyncAwait/AsyncAwait.cpp`：UI 线程调度器实现
- `MfcAsyncAwait/BizService.*`：模拟耗时业务逻辑
- `MfcAsyncAwait/MainDlg.*`：MFC 对话框示例

## 设计边界

- 该示例依赖 UI 线程消息循环，所以 `resume_ui()` 需要在应用启动时调用 `ui_dispatcher::initialize()`
- 示例重点是“不改业务函数签名和调用表达式”，不是完整任务取消框架
- 如果要扩展到多个窗口或更复杂生命周期，建议把窗口生命周期和协程取消再做一层封装