# UsageExample

这个目录提供一个最小接入样例，目标不是直接运行，而是让你把 `eventbus_core` 迁入现有项目时有一份可以直接照抄的骨架。

## 最小宿主对象

参考 `UsageExample.h` 中的 `ExampleEventBusHost`：

1. 它把 `UiDispatcher` 和 `EventBus` 组合成一个应用级上下文。
2. 你的 UI 主线程启动时调用 `initializeOnUiThread()`。
3. 业务层统一通过 `getEventBus()` 发布和订阅事件。

## 典型接入步骤

1. 在应用入口创建一个全局或应用级 `ExampleEventBusHost` 对象。
2. UI 线程初始化阶段调用 `initializeOnUiThread()`。
3. 订阅需要切回 UI 线程的事件时，使用 `DispatchPolicy::UiThread`。
4. 业务线程完成后，直接 `Publish` 事件，不需要 `PostMessage`。
5. 用 `RunExampleUiLoop` 或 `UiPumpHelper::runLoop` 接管主消息循环中的等待和调度。

## 事件发布示例

- UI 线程订阅 `ExampleTaskFinishedEvent`
- 后台线程发布 `ExampleTaskFinishedEvent`
- EventBus 自动把回调投递给 `UiDispatcher`
- `UiPumpHelper` 在 UI 线程里 `Drain` 回调并更新界面

如果你要接 MFC 工程，优先参考根目录 `templates/mfc_existing_project` 里的模板文件；如果你是纯 Win32 或自定义消息循环，可以直接参考这里的宿主对象和 `UiPumpHelper` 用法。
