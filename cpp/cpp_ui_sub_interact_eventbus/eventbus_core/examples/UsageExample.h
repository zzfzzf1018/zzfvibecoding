#pragma once

#include <string>

#include <eventbus/EventBus.h>
#include <eventbus/UiPumpHelper.h>

struct ExampleTaskFinishedEvent {
    int task_id = 0;
    std::wstring message;
};

class ExampleEventBusHost {
public:
    ExampleEventBusHost()
        : m_eventBus(m_uiDispatcher) {
    }

    bool initializeOnUiThread() {
        return m_uiDispatcher.initializeForCurrentThread();
    }

    void shutdown() {
        m_uiDispatcher.shutdown();
    }

    EventBus& getEventBus() {
        return m_eventBus;
    }

    UiDispatcher& getUiDispatcher() {
        return m_uiDispatcher;
    }

private:
    UiDispatcher m_uiDispatcher;
    EventBus m_eventBus;
};

inline int RunExampleUiLoop(
    ExampleEventBusHost& host,
    const UiPumpHelper::MessagePump& pumpMessage,
    const UiPumpHelper::QuitHandler& onQuit,
    const UiPumpHelper::WaitErrorHandler& onWaitFailed = UiPumpHelper::WaitErrorHandler()) {
    return UiPumpHelper::runLoop(host.getUiDispatcher(), pumpMessage, onQuit, onWaitFailed);
}