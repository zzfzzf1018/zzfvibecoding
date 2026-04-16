# Simple State Machine
# Simple State Machine

这是一个适用于 MSVC v141 的轻量级分层状态机示例，当前版本已经补上统一日志接口和迁移动作扩展点。

## 文件结构

- `Logger.h`: 日志接口 `ILogger` 和控制台实现 `ConsoleLogger`
- `StateMachine.h`: 状态、事件、上下文结构、状态机类声明
- `StateMachine.cpp`: 分层状态解析、状态迁移、回调与动作执行
- `main.cpp`: 演示如何接入日志、状态回调和迁移动作

## 状态层级

- `Root`
- `Idle`
- `Active`
- `Running`
- `Paused`
- `Stopped`

其中：

- `Running` 和 `Paused` 的父状态是 `Active`
- `Idle`、`Active`、`Stopped` 的父状态是 `Root`

## 事件

- `Start`
- `Pause`
- `Resume`
- `Stop`
- `Reset`

## 分层迁移表

- `Idle` + `Start` -> `Running`
- `Running` + `Pause` -> `Paused`
- `Paused` + `Resume` -> `Running`
- `Active` + `Stop` -> `Stopped`
- `Stopped` + `Reset` -> `Idle`

这里的 `Active + Stop -> Stopped` 表示 `Stop` 事件会先在叶子状态处理，如果叶子状态没有匹配迁移，就沿父状态向上冒泡到 `Active` 处理。

## 统一日志接口

状态机内部不再直接写 `std::cout`，而是通过 `ILogger` 输出日志：

```cpp
class ILogger
{
public:
	virtual ~ILogger() {}
	virtual void Log(const std::string& message) = 0;
};
```

默认示例使用 `ConsoleLogger`，你也可以换成文件日志、网络日志或业务日志系统。

## 状态回调和迁移动作

状态回调和迁移动作都统一使用 `TransitionContext`：

```cpp
struct TransitionContext
{
	State fromState;
	State toState;
	Event event;
	State handledByState;
};
```

可用接口：

```cpp
void SetStateCallbacks(State state, Callback onEnter, Callback onExit);
void SetTransitionAction(State state, Event event, Callback action);
void SetLogger(ILogger* logger);
```

说明：

- `SetStateCallbacks`: 给某个状态注册进入和退出动作
- `SetTransitionAction`: 给某个状态上的某个事件注册迁移动作
- `handledByState`: 表示本次迁移最终是在哪一层状态上被匹配处理的

## 示例行为

`main.cpp` 里演示了：

- 给父状态 `Active` 注册进入/退出回调
- 给 `Running`、`Paused`、`Stopped` 注册状态回调
- 给 `Start`、`Pause`、`Resume`、`Stop`、`Reset` 注册业务动作
- 通过日志输出完整迁移链路

## 打开方式

用 Visual Studio 2017 打开 `StateMachineDemo.sln`，确保安装了 `v141` 工具集。

## 后续扩展建议

- 把迁移表提取成配置数据，支持业务模块装配
- 为状态机增加守卫条件和动作返回值
- 如果需要并行区域或更复杂父子状态关系，再继续扩展为完整 HSM 框架