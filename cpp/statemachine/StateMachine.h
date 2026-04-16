#pragma once

#include <cstddef>
#include <functional>
#include <string>
#include <vector>

#include "Logger.h"

enum class State
{
    Root,
    Idle,
    Active,
    Running,
    Paused,
    Stopped,
    Count
};

enum class Event
{
    Start,
    Pause,
    Resume,
    Stop,
    Reset
};

const char* ToString(State state);
const char* ToString(Event event);

struct TransitionContext
{
    State fromState;
    State toState;
    Event event;
    State handledByState;
};

class StateMachine
{
public:
    typedef std::function<void(const TransitionContext&)> Callback;

    StateMachine();

    bool HandleEvent(Event event);
    State GetState() const;
    void SetLogger(ILogger* logger);
    void SetStateCallbacks(State state, Callback onEnter, Callback onExit);
    void SetTransitionAction(State state, Event event, Callback action);

private:
    struct StateHooks
    {
        Callback onEnter;
        Callback onExit;
    };

    struct TransitionActionBinding
    {
        State state;
        Event event;
        Callback action;
    };

    struct ResolvedTransition
    {
        State handledByState;
        State nextState;
    };

    static std::size_t ToIndex(State state);
    static State GetParentState(State state);
    void BuildStatePath(State state, State* path, std::size_t& count) const;
    bool TryResolveTransition(Event event, ResolvedTransition& resolvedTransition) const;
    void Log(const std::string& message) const;
    void InvokeExitCallback(State state, const TransitionContext& context) const;
    void InvokeEnterCallback(State state, const TransitionContext& context) const;
    void InvokeTransitionAction(const TransitionContext& context) const;

private:
    State currentState_;
    ILogger* logger_;
    StateHooks hooks_[static_cast<std::size_t>(State::Count)];
    std::vector<TransitionActionBinding> transitionActions_;
};