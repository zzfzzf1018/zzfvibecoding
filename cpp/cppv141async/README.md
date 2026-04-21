# cppv141async

一个面向 MSVC v141 的最小示例，演示如何在 MFC 中做出接近 C# async/await 的使用体验：

- UI 事件处理函数中使用 `co_await`
- 同步业务函数不改签名、不改实现
- 业务逻辑自动切到后台线程执行
- 执行完成后自动切回 UI 线程更新控件

## 方案要点

本示例基于 MSVC `/await` 和 `std::experimental::coroutine`。

这套 `task<T>` 对可复制业务结果类型最稳，例如 `CString`、`std::wstring`、普通值对象。
如果返回值是复杂 move-only 类型，建议额外审查生命周期和所有权转移路径。

代码结构已经拆分为三层：

- `MfcAsyncAwait/Task.h`：协程返回类型 `task<T>` 和 `fire_and_forget`
- `MfcAsyncAwait/Dispatcher.h`：后台线程与 UI 线程切换
- `MfcAsyncAwait/AsyncCall.h`：公开调用入口 `AsyncCall(...)` 和 `TryAsyncCall(...)`

新代码直接面向 `AsyncCall(...)` 与 `TryAsyncCall(...)`，不需要再关心旧的宏式包装入口。

## 每种方式的示例

### 1. 普通可调用对象

```cpp
auto text = co_await cppv141async::AsyncCall([requestId, service = m_service]() {
    return service.QuerySlowReport(requestId);
});
m_resultEdit.SetWindowTextW(text);
```

这里同步业务逻辑仍然不需要改签名，只是通过 lambda 明确把后台需要的数据按值捕获进去。

### 2. 值对象成员函数

```cpp
auto text = co_await cppv141async::AsyncCall(m_service, &BizService::QuerySlowReport, requestId);
m_resultEdit.SetWindowTextW(text);
```

### 3. `shared_ptr` 成员函数

```cpp
auto text = co_await cppv141async::AsyncCall(m_servicePtr, &BizService::QuerySlowReport, requestId);
m_resultEdit.SetWindowTextW(text);
```

这个版本会在后台任务存活期间持有 `shared_ptr`，更适合真正和生命周期绑定的业务服务。

### 4. `weak_ptr` 尝试调用

返回值不是 `void` 时，结果类型是 `std::optional<T>`：

```cpp
auto maybeText = co_await cppv141async::TryAsyncCall(m_serviceWeak, &BizService::QuerySlowReport, requestId);
if (maybeText)
{
    m_resultEdit.SetWindowTextW(*maybeText);
}
```

如果成员函数返回 `void`，结果类型是 `bool`，表示后台是否真的拿到了对象并执行了调用：

```cpp
bool invoked = co_await cppv141async::TryAsyncCall(m_workerWeak, &Worker::RefreshCache);
if (!invoked)
{
    statusText = L"Worker expired before background execution.";
}
```

### 5. 更接近 C# 的函数式调用风格

普通可调用对象：

```cpp
auto text = co_await cppv141async::AsyncCall([requestId, service = m_service]() {
    return service.QuerySlowReport(requestId);
});
```

成员函数、`shared_ptr`、`weak_ptr` 场景也统一走 `AsyncCall(...)` / `TryAsyncCall(...)`，这样公开 API 只有一层，不再需要额外记忆宏名字。

## MFC UI 窗口推荐模式

UI 窗口本身不要直接传 `this` 到后台线程。推荐把窗口句柄和生命周期标记留在主线程使用，把真正的业务对象交给后台任务：

```cpp
cppv141async::fire_and_forget CMainDlg::LoadDataAsync()
{
    const auto lifetime = lifetime_;
    const HWND dialogHandle = GetSafeHwnd();
    const int requestId = next_request_id_++;

    ::SetDlgItemTextW(dialogHandle, IDC_STATIC_STATUS, L"Loading from worker thread...");

    auto report = co_await cppv141async::AsyncCall(biz_service_, &BizService::QuerySlowReport, requestId);

    if (!lifetime->alive.load() || !::IsWindow(dialogHandle))
    {
        co_return;
    }

    ::SetDlgItemTextW(dialogHandle, IDC_EDIT_RESULT, report);
    ::SetDlgItemTextW(dialogHandle, IDC_STATIC_STATUS, L"Completed on UI thread");
}
```

这里后台线程只接触 `BizService` 和请求参数；UI 更新只发生在恢复到主线程之后，并且通过 `HWND` 和生命周期标记做防护。

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

- `MfcAsyncAwait/Task.h`：协程任务类型 `task<T>` 与 `fire_and_forget`
- `MfcAsyncAwait/Dispatcher.h` + `MfcAsyncAwait/Dispatcher.cpp`：线程切换与 UI 调度器
- `MfcAsyncAwait/AsyncCall.h`：公开异步调用入口 `AsyncCall(...)` 与 `TryAsyncCall(...)`，这是 header-only 模块
- `MfcAsyncAwait/BizService.*`：模拟耗时业务逻辑
- `MfcAsyncAwait/MainDlg.*`：MFC 对话框示例

## 设计边界

- 该示例依赖 UI 线程消息循环，所以 `resume_ui()` 需要在应用启动时调用 `ui_dispatcher::initialize()`
- 示例重点是“不改业务函数签名和调用表达式”，不是完整任务取消框架
- 如果要扩展到多个窗口或更复杂生命周期，建议把窗口生命周期和协程取消再做一层封装
