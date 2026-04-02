# MFC Existing Project Template

这个模板目录用于把 `eventbus_core` 接到已有 MFC 工程里，而不是替换你的原有架构。

## 选型

- 默认 MFC 消息循环：直接用 `AppEventBusContext.h`
- 自定义 `Run()` 主循环：用 `AppUiPumpAdapter.h`
- 显式窗口消息路由：只在你明确需要时再用 `OptionalWindowWakeAdapter.h`

## 文件

- `AppEventBusContext.h`：应用级 EventBus/UiDispatcher 上下文
- `OptionalWindowWakeAdapter.h`：显式绑定窗口消息的高级可选适配器
- `AppUiPumpAdapter.h`：自定义 `Run()` 的 `UiPumpHelper` 适配
- `FrameWindowExample.h/.cpp`：主框架窗口接入骨架
- `DialogExample.h/.cpp`：主对话框接入骨架

## 建议拷贝组合

- 默认 MFC Run，主框架窗口：`AppEventBusContext.h` + `FrameWindowExample.h/.cpp` 的对应结构
- 默认 MFC Run，主对话框：`AppEventBusContext.h` + `DialogExample.h/.cpp` 的对应结构
- 自定义主循环：`AppEventBusContext.h` + `AppUiPumpAdapter.h`
- 显式窗口消息路由：在以上基础上再加 `OptionalWindowWakeAdapter.h`

## 迁移包分层

- 基础层：`AppEventBusContext.h`
- 默认宿主层：`FrameWindowExample.h/.cpp` 或 `DialogExample.h/.cpp`
- 可选增强层：`OptionalWindowWakeAdapter.h`
- 自定义主循环层：`AppUiPumpAdapter.h`

## Checklist

1. 给工程添加头文件目录 `eventbus_core/include`。
2. 链接 `eventbus_core.lib`，或者把 `eventbus_core.vcxproj` 作为 `ProjectReference` 加入解决方案。
3. 在 `CWinApp` 子类里增加 `AppEventBusContext` 成员。
4. 在 `InitInstance()` 中调用 `initializeOnUiThread()`。
5. 二选一：
   - 默认 MFC Run：不需要额外 `ON_MESSAGE`，`UiDispatcher` 会在内部隐藏窗口上处理唤醒消息。
   - 自定义 Run：在 `Run()` 里调用 `RunMfcUiLoop(...)`。
   - 只有在你明确需要把唤醒消息路由到自己的窗口时，再选 `OptionalWindowWakeAdapter.h`。
6. 需要切回主线程的订阅统一使用 `subscribe(..., DispatchPolicy::UiThread)`。
7. 原先手写的 `PostMessage`/线程切回代码改成 `EventBus::publish(...)`。
8. 窗口关闭时先停业务线程，再清理唤醒绑定或关闭 dispatcher。

## 默认 MFC Run

1. 在 `InitInstance()` 中调用 `initializeOnUiThread()` 即可启用内部消息窗口唤醒。
2. 窗口类本身不需要增加 `ON_MESSAGE` 或 drain 处理函数。
3. 可直接参考 `FrameWindowExample.h/.cpp` 和 `DialogExample.h/.cpp`。

## 显式窗口消息路由

1. 这不是默认路径，只在你必须复用现有窗口消息通道时再使用。
2. 主窗口创建后调用 `ConfigureWindowWakeRouting(eventbus_context_, window)`。
3. 在窗口类里增加 `ON_MESSAGE(kMfcUiDispatchDrainMessage, &TWindow::OnUiDispatchDrain)`。
4. 在 `OnUiDispatchDrain(WPARAM, LPARAM)` 中调用 `HandleWindowWakeRoutingMessage(eventbus_context_, w_param, l_param)`。
5. 窗口关闭时调用 `ClearWindowWakeRouting(eventbus_context_)`。
6. 只有在窗口和 dispatcher 同属 UI 线程，且你能明确管理窗口生命周期时再用这条路。

## 推荐 API 面

- 默认接口：`initializeOnUiThread()`、`shutdown()`、`getEventBus()`
- 默认调度策略：`subscribe(..., DispatchPolicy::UiThread)` + `publish(...)`
- 默认 MFC Run：不需要 `ON_MESSAGE`，不需要显式绑定 wake window
- 高级可选接口：`configureMessageWakeup()`、`clearMessageWakeup()`
- 自定义主循环接口：`RunMfcUiLoop(...)`

## 最小公开接口集

- 应用级上下文：`AppEventBusContext`
- 默认生命周期：`initializeOnUiThread()`、`shutdown()`
- 默认事件接口：`getEventBus()`、`publish(...)`、`subscribe(..., DispatchPolicy::UiThread)`
- 默认 MFC 宿主：不需要窗口消息处理函数
- 高级可选窗口路由：`ConfigureWindowWakeRouting(...)`、`ClearWindowWakeRouting(...)`

## 默认用法 vs 高级用法

| 维度 | 默认用法 | 高级用法 |
| --- | --- | --- |
| 宿主类型 | 默认 MFC `Run()` | 显式窗口消息路由或自定义 `Run()` |
| 窗口类改动 | 通常不需要 | 可能需要 `ON_MESSAGE` 或自定义主循环 |
| 推荐模板 | `AppEventBusContext.h` + 示例骨架 | `OptionalWindowWakeAdapter.h` 或 `AppUiPumpAdapter.h` |
| 典型接口 | `initializeOnUiThread()`、`getEventBus()` | `ConfigureWindowWakeRouting(...)`、`RunMfcUiLoop(...)` |
| 唤醒控制 | 由 `UiDispatcher` 内部处理 | 由宿主显式控制 |
| 适用场景 | 新接入、最小改造、先跑通 | 需要兼容现有窗口消息体系或已有主循环 |
| 推荐程度 | 优先 | 仅在确有需要时使用 |

## 自定义 Run

1. 在 `Run()` 中调用 `RunMfcUiLoop(*this, eventbus_context_, ...)`。
2. 在 `ExitInstance()` 中调用 `eventBusContext.shutdown()`。
3. 如果你已经有自定义主循环，这条路径更贴近原有结构。
