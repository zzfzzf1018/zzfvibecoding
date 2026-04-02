#pragma once

#include <Windows.h>

#include <eventbus/EventBus.h>

class AppEventBusContext {
public:
    AppEventBusContext()
        : m_eventBus(m_uiDispatcher) {
    }

    bool initializeOnUiThread() {
        return m_uiDispatcher.initializeForCurrentThread();
    }

    void shutdown() {
        m_uiDispatcher.shutdown();
    }

    void configureMessageWakeup(HWND wakeWindow, UINT wakeMessage) {
        m_uiDispatcher.setMessageTarget(wakeWindow, wakeMessage);
    }

    void clearMessageWakeup() {
        m_uiDispatcher.clearMessageTarget();
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