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
- `cppv141async::background_invoke_member(...)`：把成员函数调用所需对象、副本参数一起固化到后台任务
- `cppv141async::background_invoke_shared_member(...)`：用 `shared_ptr` 托管后台任务中的服务对象生命周期
- `cppv141async::background_invoke_weak_member(...)`：用 `weak_ptr` 尝试调用对象成员，失效时安全跳过
- `cppv141async::AsyncCall(...)`：更接近 C# 调用风格的函数式入口
- `cppv141async::TryAsyncCall(...)`：针对 `weak_ptr` 的安全尝试调用入口
- `CPPV141_ASYNC(expr)`：把原来的同步调用表达式直接包装成可 `co_await` 的异步调用
- `CPPV141_ASYNC_MEMBER(Type, object, method, ...)`：更适合成员函数调用的安全包装
- `CPPV141_ASYNC_MEMBER_AUTO(object, method, ...)`：自动推导对象类型的成员调用包装
- `CPPV141_ASYNC_SHARED_MEMBER(sharedPtr, method, ...)`：面向 `shared_ptr` 服务对象的成员调用包装
- `CPPV141_ASYNC_WEAK_MEMBER(weakPtr, method, ...)`：面向 `weak_ptr` 服务对象的成员调用包装

## 每种方式的示例

### 1. 普通表达式包装

```cpp
auto text = co_await CPPV141_ASYNC(m_service.QuerySlowReport(requestId));
m_resultEdit.SetWindowTextW(text);
```

这里 `m_service.QuerySlowReport(requestId)` 仍然是同步函数，不需要改成回调、future、promise 或额外的异步版本。

### 2. 显式类型的成员函数包装

```cpp
auto text = co_await CPPV141_ASYNC_MEMBER(BizService, m_service, QuerySlowReport, requestId);
m_resultEdit.SetWindowTextW(text);
```

### 3. 自动推导类型的成员函数包装

```cpp
auto text = co_await CPPV141_ASYNC_MEMBER_AUTO(m_service, QuerySlowReport, requestId);
m_resultEdit.SetWindowTextW(text);
```

这个 helper 会把 `m_service` 和 `requestId` 都按值固化到后台任务里，减少因为 `this` 或局部引用跨线程失效导致的崩溃。

### 4. `shared_ptr` 成员函数包装

```cpp
auto text = co_await CPPV141_ASYNC_SHARED_MEMBER(m_servicePtr, QuerySlowReport, requestId);
m_resultEdit.SetWindowTextW(text);
```

这个版本会在后台任务存活期间持有 `shared_ptr`，更适合真正和生命周期绑定的业务服务。

### 5. `weak_ptr` 成员函数包装

返回值不是 `void` 时，结果类型是 `std::optional<T>`：

```cpp
auto maybeText = co_await CPPV141_ASYNC_WEAK_MEMBER(m_serviceWeak, QuerySlowReport, requestId);
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

### 6. 更接近 C# 的函数式调用风格

普通可调用对象：

```cpp
auto text = co_await cppv141async::AsyncCall([requestId, service = m_service]() {
    return service.QuerySlowReport(requestId);
});
```

成员函数：

```cpp
auto text = co_await cppv141async::AsyncCall(m_service, &BizService::QuerySlowReport, requestId);
```

`shared_ptr` 成员函数：

```cpp
auto text = co_await cppv141async::AsyncCall(m_servicePtr, &BizService::QuerySlowReport, requestId);
```

`weak_ptr` 尝试调用：

```cpp
auto maybeText = co_await cppv141async::TryAsyncCall(m_serviceWeak, &BizService::QuerySlowReport, requestId);
```

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

- `MfcAsyncAwait/AsyncAwait.h`：协程任务类型、线程切换 awaiter、辅助宏
- `MfcAsyncAwait/AsyncAwait.cpp`：UI 线程调度器实现
- `MfcAsyncAwait/BizService.*`：模拟耗时业务逻辑
- `MfcAsyncAwait/MainDlg.*`：MFC 对话框示例

## 设计边界

- 该示例依赖 UI 线程消息循环，所以 `resume_ui()` 需要在应用启动时调用 `ui_dispatcher::initialize()`
- 示例重点是“不改业务函数签名和调用表达式”，不是完整任务取消框架
- 如果要扩展到多个窗口或更复杂生命周期，建议把窗口生命周期和协程取消再做一层封装
