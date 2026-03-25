#include <afxwin.h>

#include "MainFrame.h"

class CMfcAsyncAwaitApp : public CWinApp {
public:
    BOOL InitInstance() override {
        CWinApp::InitInstance();

        auto* frame = new CMainFrame();
        m_pMainWnd = frame;

        frame->ShowWindow(SW_SHOW);
        frame->UpdateWindow();
        return TRUE;
    }
};

CMfcAsyncAwaitApp theApp;
