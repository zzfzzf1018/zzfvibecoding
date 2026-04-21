#pragma once

#include <afxwin.h>
#include <afxdialogex.h>

#include "AsyncCall.h"
#include "BizService.h"
#include "resource.h"

#include <atomic>
#include <memory>

class CMainDlg : public CDialogEx
{
public:
    CMainDlg();
    virtual ~CMainDlg();

    enum
    {
        IDD = IDD_MFCASYNC_DIALOG
    };

protected:
    virtual void DoDataExchange(CDataExchange* data_exchange);
    virtual BOOL OnInitDialog();
    afx_msg void OnBnClickedLoad();

    DECLARE_MESSAGE_MAP()

private:
    cppv141async::fire_and_forget LoadDataAsync();

    struct lifetime_flag
    {
        std::atomic<bool> alive { true };
    };

private:
    CButton load_button_;
    CStatic status_static_;
    CEdit result_edit_;
    BizService biz_service_;
    int next_request_id_;
    std::shared_ptr<lifetime_flag> lifetime_;
};