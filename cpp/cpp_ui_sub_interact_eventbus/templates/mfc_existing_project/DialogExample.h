#pragma once

#include <afxwin.h>

#include "AppEventBusContext.h"

class CMainDialog : public CDialogEx {
public:
    explicit CMainDialog(AppEventBusContext& eventBusContext);

protected:
    BOOL OnInitDialog() override;
    afx_msg void OnDestroy();

    DECLARE_MESSAGE_MAP()

private:
    AppEventBusContext& m_eventBusContext;
};
