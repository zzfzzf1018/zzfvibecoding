# cpp_ui_sub_interact_eventbus

这是一个 VS2017 v141 的 MFC 示例工程，演示“后台线程发布事件，UI 线程统一消费并更新界面”。

当前默认路径：

- `UiDispatcher` 在 UI 线程内部创建隐藏消息窗口做唤醒
- `EventBus::publish(...)` 从任意线程发布事件
- `subscribe(..., DispatchPolicy::UiThread)` 自动把处理器切回 UI 线程
- 默认 MFC 消息循环下不需要 `ON_MESSAGE`，也不需要重写 `Run()`

## 适合谁

- 想把手写 `PostMessage`/回调切回逻辑收敛到统一事件总线的 MFC 工程
- 需要保留“默认 MFC Run”和“自定义主循环”两种接入方式的宿主
- 希望把可复用部分抽成静态库，后续迁入现有项目

## 默认接入

1. 在 UI 线程启动阶段调用 `UiDispatcher::initializeForCurrentThread()`。
2. 用 `subscribe(..., DispatchPolicy::UiThread)` 注册 UI 消费者。
3. 在后台线程调用 `publish(...)`。
4. 默认 MFC 消息循环下不需要 `ON_MESSAGE`。

## 接入文档

- 库说明：`eventbus_core/README.md`
- MFC 迁移模板：`templates/mfc_existing_project/README.md`
- 最小示例骨架：`eventbus_core/examples/UsageExample.h` 和 `eventbus_core/examples/UsageExample.md`

## 最终推荐 API 面

- 默认初始化：`UiDispatcher::initializeForCurrentThread()`
- 默认关闭：`UiDispatcher::shutdown()`
- 默认发布/订阅：`EventBus::publish(...)` + `subscribe(..., DispatchPolicy::UiThread)`
- 高级可选模式：`UiDispatcher::setMessageTarget(...)` / `clearMessageTarget()`
- 自定义主循环模式：`UiPumpHelper::runLoop(...)`

## 默认用法 vs 高级用法

| 维度 | 默认用法 | 高级用法 |
| --- | --- | --- |
| UI 唤醒方式 | `UiDispatcher` 内部隐藏消息窗口 | 显式窗口消息路由或 event handle |
| MFC 默认消息循环 | 直接支持 | 仅在你明确需要控制唤醒路径时使用 |
| 是否需要 `ON_MESSAGE` | 不需要 | 显式窗口消息路由时需要 |
| 是否需要自定义 `Run()` | 不需要 | 自定义主循环模式需要 |
| 推荐接口 | `initializeForCurrentThread()` + `publish(...)` + `subscribe(..., DispatchPolicy::UiThread)` | `setMessageTarget(...)` / `clearMessageTarget()` 或 `UiPumpHelper::runLoop(...)` |

## 示例工程

- 示例应用入口：`UiEventBusApp.cpp`
- 示例窗口：`MainFrame.cpp`
- 静态库工程：`eventbus_core/eventbus_core.vcxproj`

## 构建

1. 使用 Visual Studio 2017 打开解决方案。
2. 确保安装了 MFC 组件和 v141 工具集。
3. 编译解决方案，先生成 `eventbus_core.lib`，再链接生成 `cpp_ui_sub_interact_eventbus.exe`。
