#pragma once

#include <afxwin.h>
#include <afxcmn.h>

#include <memory>
#include <optional>

#include "AsyncAwait.h"

class CMainFrame : public CFrameWnd {
public:
    CMainFrame();

protected:
    afx_msg int OnCreate(LPCREATESTRUCT lpCreateStruct);
    afx_msg void OnPaint();
    afx_msg void OnSize(UINT nType, int cx, int cy);
    afx_msg void OnStartButtonClicked();
    afx_msg void OnChainButtonClicked();
    afx_msg void OnCancelButtonClicked();
    afx_msg LRESULT OnAsyncContinuation(WPARAM wParam, LPARAM lParam);

    DECLARE_MESSAGE_MAP()

private:
    void AppendLog(const CString& line);
    void SetRunningState(bool running);
    void FinishWithResult(const std::optional<CString>& result, std::exception_ptr ep);
    void LayoutControls();
    UINT GetCurrentDpi() const;
    int ScaleByDpi(int value) const;

private:
    enum {
        IDC_BTN_START = 1001,
        IDC_BTN_CHAIN = 1002,
        IDC_BTN_CANCEL = 1003,
        IDC_PROGRESS = 1004,
        IDC_EDIT_LOG = 1005
    };

    CButton m_btnStart;
    CButton m_btnChain;
    CButton m_btnCancel;
    CProgressCtrl m_progress;
    CEdit m_log;
    std::shared_ptr<async_mfc::UiContinuationQueue> m_uiQueue;
    std::optional<async_mfc::CancellationTokenSource> m_cancelSource;
    bool m_isRunning = false;
};
