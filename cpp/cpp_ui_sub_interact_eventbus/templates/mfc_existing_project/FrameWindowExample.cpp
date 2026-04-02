#include "FrameWindowExample.h"

BOOL CMyApp::InitInstance() {
    CWinApp::InitInstance();

    if (!m_eventBusContext.initializeOnUiThread()) {
        return FALSE;
    }

    auto* frame = new CMainFrame();
    m_pMainWnd = frame;

    frame->ShowWindow(SW_SHOW);
    frame->UpdateWindow();
    return TRUE;
}

int CMyApp::ExitInstance() {
    m_eventBusContext.shutdown();
    return CWinApp::ExitInstance();
}

CMainFrame::CMainFrame() {
    Create(nullptr, _T("Frame Window Example"));
}

void CMainFrame::OnClose() {
    CFrameWnd::OnClose();
}

BEGIN_MESSAGE_MAP(CMainFrame, CFrameWnd)
    ON_WM_CLOSE()
END_MESSAGE_MAP()