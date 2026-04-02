#include "pch.h"

#include "UiEventBusApp.h"
#include "MainFrame.h"

CUiEventBusApp::CUiEventBusApp()
    : m_eventBus(m_uiDispatcher) {
}

BOOL CUiEventBusApp::InitInstance() {
    CWinApp::InitInstance();

    if (!m_uiDispatcher.initializeForCurrentThread()) {
        AfxMessageBox(_T("UiDispatcher initialization failed."));
        return FALSE;
    }

    auto* frame = new CMainFrame();
    m_pMainWnd = frame;
    frame->ShowWindow(SW_SHOW);
    frame->UpdateWindow();
    return TRUE;
}

int CUiEventBusApp::ExitInstance() {
    m_uiDispatcher.shutdown();
    return CWinApp::ExitInstance();
}

CUiEventBusApp& GetApp() {
    return theApp;
}