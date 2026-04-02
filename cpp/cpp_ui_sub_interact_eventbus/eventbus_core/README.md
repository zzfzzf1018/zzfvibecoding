# eventbus_core

`eventbus_core` 是这个仓库里的可复用静态库，负责把“事件发布/订阅”和“切回 UI 线程执行”这两件事抽成独立能力。

## 解决的问题

- 业务线程发布事件，不直接碰 UI
- 需要切回 UI 线程的处理器统一走 `DispatchPolicy::UiThread`
- 默认 MFC 消息循环下，不需要自己接 `ON_MESSAGE` 或重写 `Run()`
- 如果宿主已经有自定义主循环，仍然可以走 event handle + `UiPumpHelper`

## 核心类型

- `UiDispatcher`：UI 线程任务队列和唤醒机制
- `EventBus`：类型安全的发布/订阅中心
- `SubscriptionToken`：订阅生命周期句柄
- `UiPumpHelper`：自定义主循环场景下的等待辅助类

## 默认用法

1. 在 UI 线程启动阶段调用 `UiDispatcher::initializeForCurrentThread()`。
2. 用 `EventBus::subscribe(..., DispatchPolicy::UiThread)` 注册需要切回 UI 的处理器。
3. 在后台线程调用 `EventBus::publish(...)`。
4. UI 回调会通过 `UiDispatcher` 在 UI 线程执行。

### 最小代码片段

```cpp
UiDispatcher ui_dispatcher;
ui_dispatcher.initializeForCurrentThread();

EventBus event_bus(ui_dispatcher);

struct TaskFinishedEvent {
    int task_id = 0;
};

auto token = event_bus.subscribe<TaskFinishedEvent>(
    [](const TaskFinishedEvent& event) {
        // update UI here
    },
    DispatchPolicy::UiThread);

std::thread worker([&event_bus]() {
    event_bus.publish(TaskFinishedEvent{42});
});

worker.join();
```

如果宿主是默认 MFC 消息循环，通常不需要额外消息处理代码；如果宿主自己接管了主循环，再配合 `UiPumpHelper::runLoop(...)` 使用。

## 唤醒模式

### 默认模式

- `UiDispatcher` 在 UI 线程创建内部隐藏消息窗口
- 适合默认 MFC 消息循环
- 不需要额外 `ON_MESSAGE`

### 高级模式

- 可以通过 `setMessageTarget(...)` 显式绑定到宿主窗口消息
- 适合必须复用现有窗口消息通道的场景

### 自定义主循环模式

- 通过 `getWakeHandle()` 暴露 event handle
- 配合 `UiPumpHelper::runLoop(...)` 使用

## 入口头文件

- `include/eventbus/UiDispatcher.h`
- `include/eventbus/EventBus.h`
- `include/eventbus/SubscriptionToken.h`
- `include/eventbus/UiPumpHelper.h`

## 配套文档

- 根示例说明：`../README.md`
- MFC 迁移模板：`../templates/mfc_existing_project/README.md`
- 最小示例骨架：`examples/UsageExample.h` 和 `examples/UsageExample.md`
