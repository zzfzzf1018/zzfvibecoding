#pragma once

#include <eventbus/EventBus.h>

class CUiEventBusApp : public CWinApp {
public:
    CUiEventBusApp();

    BOOL InitInstance() override;
    int ExitInstance() override;

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

extern CUiEventBusApp theApp;

CUiEventBusApp& GetApp();