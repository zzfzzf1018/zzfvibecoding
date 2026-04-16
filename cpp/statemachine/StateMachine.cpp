#include "StateMachine.h"

#include <sstream>

namespace
{
    struct Transition
    {
        State from;
        Event event;
        State to;
    };

    const Transition kTransitions[] = {
        { State::Idle, Event::Start, State::Running },
        { State::Running, Event::Pause, State::Paused },
        { State::Paused, Event::Resume, State::Running },
        { State::Active, Event::Stop, State::Stopped },
        { State::Stopped, Event::Reset, State::Idle }
    };
}

const char* ToString(State state)
{
    switch (state)
    {
    case State::Root:
        return "Root";
    case State::Idle:
        return "Idle";
    case State::Active:
        return "Active";
    case State::Running:
        return "Running";
    case State::Paused:
        return "Paused";
    case State::Stopped:
        return "Stopped";
    case State::Count:
        return "Count";
    default:
        return "UnknownState";
    }
}

const char* ToString(Event event)
{
    switch (event)
    {
    case Event::Start:
        return "Start";
    case Event::Pause:
        return "Pause";
    case Event::Resume:
        return "Resume";
    case Event::Stop:
        return "Stop";
    case Event::Reset:
        return "Reset";
    default:
        return "UnknownEvent";
    }
}

StateMachine::StateMachine()
    : currentState_(State::Idle)
    , logger_(NULL)
{
}

bool StateMachine::HandleEvent(Event event)
{
    ResolvedTransition resolvedTransition;
    if (!TryResolveTransition(event, resolvedTransition))
    {
        std::ostringstream stream;
        stream << "忽略事件: " << ToString(event)
               << ", 当前状态: " << ToString(currentState_);
        Log(stream.str());
        return false;
    }

    TransitionContext context;
    context.fromState = currentState_;
    context.toState = resolvedTransition.nextState;
    context.event = event;
    context.handledByState = resolvedTransition.handledByState;

    {
        std::ostringstream stream;
        stream << "状态迁移: " << ToString(context.fromState)
               << " --(" << ToString(context.event)
               << ", handled by " << ToString(context.handledByState)
               << ")--> " << ToString(context.toState);
        Log(stream.str());
    }

    State fromPath[static_cast<std::size_t>(State::Count)];
    State toPath[static_cast<std::size_t>(State::Count)];
    std::size_t fromCount = 0;
    std::size_t toCount = 0;

    BuildStatePath(context.fromState, fromPath, fromCount);
    BuildStatePath(context.toState, toPath, toCount);

    while (fromCount > 0 && toCount > 0 && fromPath[fromCount - 1] == toPath[toCount - 1])
    {
        --fromCount;
        --toCount;
    }

    for (std::size_t index = 0; index < fromCount; ++index)
    {
        InvokeExitCallback(fromPath[index], context);
    }

    InvokeTransitionAction(context);

    currentState_ = context.toState;

    while (toCount > 0)
    {
        --toCount;
        InvokeEnterCallback(toPath[toCount], context);
    }

    return true;
}

State StateMachine::GetState() const
{
    return currentState_;
}

void StateMachine::SetLogger(ILogger* logger)
{
    logger_ = logger;
}

void StateMachine::SetStateCallbacks(State state, Callback onEnter, Callback onExit)
{
    const std::size_t index = ToIndex(state);
    hooks_[index].onEnter = onEnter;
    hooks_[index].onExit = onExit;
}

void StateMachine::SetTransitionAction(State state, Event event, Callback action)
{
    for (std::size_t index = 0; index < transitionActions_.size(); ++index)
    {
        TransitionActionBinding& binding = transitionActions_[index];
        if (binding.state == state && binding.event == event)
        {
            binding.action = action;
            return;
        }
    }

    TransitionActionBinding binding;
    binding.state = state;
    binding.event = event;
    binding.action = action;
    transitionActions_.push_back(binding);
}

std::size_t StateMachine::ToIndex(State state)
{
    return static_cast<std::size_t>(state);
}

State StateMachine::GetParentState(State state)
{
    switch (state)
    {
    case State::Idle:
        return State::Root;
    case State::Active:
        return State::Root;
    case State::Running:
        return State::Active;
    case State::Paused:
        return State::Active;
    case State::Stopped:
        return State::Root;
    case State::Root:
    case State::Count:
    default:
        return State::Root;
    }
}

void StateMachine::BuildStatePath(State state, State* path, std::size_t& count) const
{
    count = 0;
    for (;;)
    {
        path[count] = state;
        ++count;

        if (state == State::Root)
        {
            break;
        }

        state = GetParentState(state);
    }
}

bool StateMachine::TryResolveTransition(Event event, ResolvedTransition& resolvedTransition) const
{
    State probeState = currentState_;
    while (true)
    {
        const std::size_t transitionCount = sizeof(kTransitions) / sizeof(kTransitions[0]);
        for (std::size_t index = 0; index < transitionCount; ++index)
        {
            const Transition& transition = kTransitions[index];
            if (transition.from == probeState && transition.event == event)
            {
                resolvedTransition.handledByState = probeState;
                resolvedTransition.nextState = transition.to;
                return true;
            }
        }

        if (probeState == State::Root)
        {
            break;
        }

        probeState = GetParentState(probeState);
    }

    return false;
}

void StateMachine::Log(const std::string& message) const
{
    if (logger_ != NULL)
    {
        logger_->Log(message);
    }
}

void StateMachine::InvokeExitCallback(State state, const TransitionContext& context) const
{
    const Callback& callback = hooks_[ToIndex(state)].onExit;
    if (callback)
    {
        callback(context);
    }
}

void StateMachine::InvokeEnterCallback(State state, const TransitionContext& context) const
{
    const Callback& callback = hooks_[ToIndex(state)].onEnter;
    if (callback)
    {
        callback(context);
    }
}

void StateMachine::InvokeTransitionAction(const TransitionContext& context) const
{
    for (std::size_t index = 0; index < transitionActions_.size(); ++index)
    {
        const TransitionActionBinding& binding = transitionActions_[index];
        if (binding.state == context.handledByState && binding.event == context.event && binding.action)
        {
            binding.action(context);
            return;
        }
    }
}