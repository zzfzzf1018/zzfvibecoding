#include "MfcAsyncAwait.h"

#include "AsyncAwait.h"
#include "MainDlg.h"

CMfcAsyncAwaitApp theApp;

BOOL CMfcAsyncAwaitApp::InitInstance()
{
    CWinApp::InitInstance();
    AfxEnableControlContainer();

    if (!cppv141async::ui_dispatcher::instance().initialize(AfxGetInstanceHandle()))
    {
        AfxMessageBox(L"Failed to initialize UI dispatcher.");
        return FALSE;
    }

    CMainDlg dialog;
    m_pMainWnd = &dialog;
    dialog.DoModal();

    cppv141async::ui_dispatcher::instance().shutdown();
    return FALSE;
}