#include "DialogExample.h"

CMainDialog::CMainDialog(AppEventBusContext& eventBusContext)
    : CDialogEx(IDD_MAIN_DIALOG),
    m_eventBusContext(eventBusContext) {
}

BOOL CMainDialog::OnInitDialog() {
    CDialogEx::OnInitDialog();
    return TRUE;
}

void CMainDialog::OnDestroy() {
    CDialogEx::OnDestroy();
}

BEGIN_MESSAGE_MAP(CMainDialog, CDialogEx)
    ON_WM_DESTROY()
END_MESSAGE_MAP()