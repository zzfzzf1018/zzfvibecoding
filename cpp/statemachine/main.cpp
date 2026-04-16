#include <iostream>
#include <string>
#include <vector>

#include "Logger.h"
#include "StateMachine.h"

namespace
{
    std::string BuildMessage(const char* prefix, const TransitionContext& context)
    {
        std::string message(prefix);
        message += ": from=";
        message += ToString(context.fromState);
        message += ", to=";
        message += ToString(context.toState);
        message += ", event=";
        message += ToString(context.event);
        message += ", handledBy=";
        message += ToString(context.handledByState);
        return message;
    }
}

int main()
{
    ConsoleLogger logger;
    StateMachine machine;
    machine.SetLogger(&logger);

    machine.SetStateCallbacks(
        State::Active,
        [&logger](const TransitionContext& context)
        {
            logger.Log(BuildMessage("进入父状态 Active", context));
        },
        [&logger](const TransitionContext& context)
        {
            logger.Log(BuildMessage("退出父状态 Active", context));
        });

    machine.SetStateCallbacks(
        State::Running,
        [&logger](const TransitionContext& context)
        {
            logger.Log(BuildMessage("进入 Running", context));
        },
        [&logger](const TransitionContext& context)
        {
            logger.Log(BuildMessage("退出 Running", context));
        });

    machine.SetStateCallbacks(
        State::Paused,
        [&logger](const TransitionContext& context)
        {
            logger.Log(BuildMessage("进入 Paused", context));
        },
        [&logger](const TransitionContext& context)
        {
            logger.Log(BuildMessage("退出 Paused", context));
        });

    machine.SetStateCallbacks(
        State::Stopped,
        [&logger](const TransitionContext& context)
        {
            logger.Log(BuildMessage("进入 Stopped", context));
        },
        [&logger](const TransitionContext& context)
        {
            logger.Log(BuildMessage("退出 Stopped", context));
        });

    machine.SetTransitionAction(
        State::Idle,
        Event::Start,
        [&logger](const TransitionContext& context)
        {
            logger.Log(BuildMessage("执行启动动作: 初始化资源", context));
        });

    machine.SetTransitionAction(
        State::Running,
        Event::Pause,
        [&logger](const TransitionContext& context)
        {
            logger.Log(BuildMessage("执行暂停动作: 挂起任务", context));
        });

    machine.SetTransitionAction(
        State::Paused,
        Event::Resume,
        [&logger](const TransitionContext& context)
        {
            logger.Log(BuildMessage("执行恢复动作: 继续任务", context));
        });

    machine.SetTransitionAction(
        State::Active,
        Event::Stop,
        [&logger](const TransitionContext& context)
        {
            logger.Log(BuildMessage("执行停止动作: 统一释放运行态资源", context));
        });

    machine.SetTransitionAction(
        State::Stopped,
        Event::Reset,
        [&logger](const TransitionContext& context)
        {
            logger.Log(BuildMessage("执行复位动作: 清理停止态", context));
        });

    const std::vector<Event> events = {
        Event::Start,
        Event::Pause,
        Event::Resume,
        Event::Stop,
        Event::Reset,
        Event::Pause
    };

    logger.Log(std::string("初始状态: ") + ToString(machine.GetState()));

    for (std::size_t index = 0; index < events.size(); ++index)
    {
        logger.Log(std::string("事件 ") + std::to_string(index + 1) + ": " + ToString(events[index]));
        machine.HandleEvent(events[index]);
    }

    logger.Log(std::string("最终状态: ") + ToString(machine.GetState()));
    return 0;
}