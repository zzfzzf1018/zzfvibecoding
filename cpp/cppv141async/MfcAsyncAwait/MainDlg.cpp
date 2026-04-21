#include "MainDlg.h"

#include <stdexcept>

BEGIN_MESSAGE_MAP(CMainDlg, CDialogEx)
    ON_BN_CLICKED(IDC_BUTTON_LOAD, &CMainDlg::OnBnClickedLoad)
END_MESSAGE_MAP()

CMainDlg::CMainDlg()
    : CDialogEx(IDD_MFCASYNC_DIALOG)
    , next_request_id_(1)
    , lifetime_(std::make_shared<lifetime_flag>())
{
}

CMainDlg::~CMainDlg()
{
    lifetime_->alive.store(false);
}

void CMainDlg::DoDataExchange(CDataExchange* data_exchange)
{
    CDialogEx::DoDataExchange(data_exchange);
    DDX_Control(data_exchange, IDC_BUTTON_LOAD, load_button_);
    DDX_Control(data_exchange, IDC_STATIC_STATUS, status_static_);
    DDX_Control(data_exchange, IDC_EDIT_RESULT, result_edit_);
}

BOOL CMainDlg::OnInitDialog()
{
    CDialogEx::OnInitDialog();

    status_static_.SetWindowTextW(L"Ready");
    result_edit_.SetWindowTextW(L"UI thread is idle.");

    return TRUE;
}

void CMainDlg::OnBnClickedLoad()
{
    LoadDataAsync();
}

cppv141async::fire_and_forget CMainDlg::LoadDataAsync()
{
    const auto lifetime = lifetime_;
    const HWND dialog_handle = GetSafeHwnd();
    const int request_id = next_request_id_++;

    load_button_.EnableWindow(FALSE);
    status_static_.SetWindowTextW(L"Loading from worker thread...");
    result_edit_.SetWindowTextW(L"Waiting for business data...");

    try
    {
        auto report = co_await cppv141async::AsyncCall(biz_service_, &BizService::QuerySlowReport, request_id);

        if (!lifetime->alive.load() || !::IsWindow(dialog_handle))
        {
            co_return;
        }

        CString status;
        status.Format(L"Completed on UI thread %lu", ::GetCurrentThreadId());

        CString output;
        output.Append(L"Business result\r\n\r\n");
        output.Append(report);

        ::SetDlgItemTextW(dialog_handle, IDC_STATIC_STATUS, status);
        ::SetDlgItemTextW(dialog_handle, IDC_EDIT_RESULT, output);
        ::EnableWindow(::GetDlgItem(dialog_handle, IDC_BUTTON_LOAD), TRUE);
    }
    catch (const std::exception& ex)
    {
        if (lifetime->alive.load() && ::IsWindow(dialog_handle))
        {
            CString message;
            message.Format(L"Error: %S", ex.what());
            ::SetDlgItemTextW(dialog_handle, IDC_STATIC_STATUS, L"Failed");
            ::SetDlgItemTextW(dialog_handle, IDC_EDIT_RESULT, message);
            ::EnableWindow(::GetDlgItem(dialog_handle, IDC_BUTTON_LOAD), TRUE);
        }
    }
    catch (...)
    {
        if (lifetime->alive.load() && ::IsWindow(dialog_handle))
        {
            ::SetDlgItemTextW(dialog_handle, IDC_STATIC_STATUS, L"Failed");
            ::SetDlgItemTextW(dialog_handle, IDC_EDIT_RESULT, L"Unknown exception.");
            ::EnableWindow(::GetDlgItem(dialog_handle, IDC_BUTTON_LOAD), TRUE);
        }
    }
}