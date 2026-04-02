#pragma once

#include <afxwin.h>

#include "AppEventBusContext.h"

class CMainFrame;

class CMyApp : public CWinApp {
public:
    BOOL InitInstance() override;
    int ExitInstance() override;

    AppEventBusContext& getEventBusContext() {
        return m_eventBusContext;
    }

private:
    AppEventBusContext m_eventBusContext;
};

class CMainFrame : public CFrameWnd {
public:
    CMainFrame();

protected:
    afx_msg void OnClose();

    DECLARE_MESSAGE_MAP()

private:
    AppEventBusContext& getAppContext() {
        return static_cast<CMyApp*>(AfxGetApp())->getEventBusContext();
    }
};
