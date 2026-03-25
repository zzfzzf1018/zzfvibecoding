#include "MainFrame.h"

#include <chrono>
#include <thread>

namespace {
int MaxInt(int a, int b) {
    return a > b ? a : b;
}
} // namespace

BEGIN_MESSAGE_MAP(CMainFrame, CFrameWnd)
    ON_WM_CREATE()
    ON_WM_PAINT()
    ON_WM_SIZE()
    ON_CONTROL(BN_CLICKED, IDC_BTN_START, &CMainFrame::OnStartButtonClicked)
    ON_CONTROL(BN_CLICKED, IDC_BTN_CHAIN, &CMainFrame::OnChainButtonClicked)
    ON_CONTROL(BN_CLICKED, IDC_BTN_CANCEL, &CMainFrame::OnCancelButtonClicked)
    ON_MESSAGE(async_mfc::WM_ASYNC_CONTINUATION, &CMainFrame::OnAsyncContinuation)
END_MESSAGE_MAP()

CMainFrame::CMainFrame() {
    CString className = AfxRegisterWndClass(CS_HREDRAW | CS_VREDRAW);
    Create(className, _T("MFC Async/Await Demo"), WS_OVERLAPPEDWINDOW,
           CRect(120, 120, 980, 640));
}

int CMainFrame::OnCreate(LPCREATESTRUCT lpCreateStruct) {
    if (CFrameWnd::OnCreate(lpCreateStruct) == -1) {
        return -1;
    }

    m_btnStart.Create(_T("执行单任务"), WS_CHILD | WS_VISIBLE | BS_PUSHBUTTON,
                      CRect(0, 0, 0, 0), this, IDC_BTN_START);

    m_btnChain.Create(_T("执行链式任务(A->B)"), WS_CHILD | WS_VISIBLE | BS_PUSHBUTTON,
                      CRect(0, 0, 0, 0), this, IDC_BTN_CHAIN);

    m_btnCancel.Create(_T("取消任务"), WS_CHILD | WS_VISIBLE | BS_PUSHBUTTON,
                       CRect(0, 0, 0, 0), this, IDC_BTN_CANCEL);
    m_btnCancel.EnableWindow(FALSE);

    m_progress.Create(WS_CHILD | WS_VISIBLE | PBS_SMOOTH,
                      CRect(0, 0, 0, 0), this, IDC_PROGRESS);
    m_progress.SetRange(0, 100);
    m_progress.SetPos(0);

    m_log.Create(WS_CHILD | WS_VISIBLE | WS_BORDER | ES_MULTILINE | ES_AUTOVSCROLL |
                     ES_READONLY | WS_VSCROLL,
                 CRect(0, 0, 0, 0),
                 this, IDC_EDIT_LOG);

    m_uiQueue = std::make_shared<async_mfc::UiContinuationQueue>();
    m_uiQueue->Attach(m_hWnd);
    AppendLog(_T("支持两种模式：单任务和链式任务(A->B)。均可取消且不卡 UI。"));
    LayoutControls();
    return 0;
}

void CMainFrame::OnPaint() {
    CPaintDC dc(this);
    dc.SetBkMode(TRANSPARENT);
    int margin = ScaleByDpi(12);
    int textY = ScaleByDpi(10);
    dc.TextOutW(margin, textY, _T("模拟 C# async/await：单任务/链式任务 + 取消 + 进度 + UI 续体"));
}

void CMainFrame::OnSize(UINT nType, int cx, int cy) {
    CFrameWnd::OnSize(nType, cx, cy);
    LayoutControls();
}

void CMainFrame::OnStartButtonClicked() {
    if (m_isRunning) {
        return;
    }

    SetRunningState(true);
    m_cancelSource.emplace();
    m_progress.SetPos(0);
    AppendLog(async_mfc::NowTimeText() + _T(" [UI] 开始执行单任务..."));

    async_mfc::AwaitCancellableProgress(
        m_uiQueue,
        m_cancelSource->Token(),
        [](const async_mfc::CancellationToken& token,
           const std::function<void(int)>& reportProgress) -> CString {
            long long sum = 0;
            for (int i = 1; i <= 100; ++i) {
                async_mfc::ThrowIfCancellationRequested(token);

                std::this_thread::sleep_for(std::chrono::milliseconds(50));
                for (int j = 1; j <= 3000; ++j) {
                    sum += (i * j) % 97;
                }

                reportProgress(i);
            }

            CString result;
            result.Format(_T("后台计算完成，sum=%lld"), sum);
            return result;
        },
        [this](int percent) {
            m_progress.SetPos(percent);
        },
        [this](std::optional<CString> result, std::exception_ptr ep) {
            FinishWithResult(result, ep);
        });
}

void CMainFrame::OnChainButtonClicked() {
    if (m_isRunning) {
        return;
    }

    SetRunningState(true);
    m_cancelSource.emplace();
    m_progress.SetPos(0);
    AppendLog(async_mfc::NowTimeText() + _T(" [UI] 开始链式任务 A -> B..."));

    async_mfc::CancellationToken token = m_cancelSource->Token();
    async_mfc::AwaitCancellableProgress(
        m_uiQueue,
        token,
        [](const async_mfc::CancellationToken& token,
           const std::function<void(int)>& reportProgress) -> CString {
            long long valueA = 1;
            for (int i = 1; i <= 50; ++i) {
                async_mfc::ThrowIfCancellationRequested(token);
                std::this_thread::sleep_for(std::chrono::milliseconds(35));
                valueA = (valueA * 131 + i * 17) % 1000000007;
                reportProgress(i);
            }

            CString stepA;
            stepA.Format(_T("阶段A完成，valueA=%lld"), valueA);
            return stepA;
        },
        [this](int percent) {
            m_progress.SetPos(percent);
        },
        [this, token](std::optional<CString> resultA, std::exception_ptr epA) {
            if (epA) {
                FinishWithResult(resultA, epA);
                return;
            }

            if (resultA.has_value()) {
                AppendLog(async_mfc::NowTimeText() + _T(" [UI] ") + resultA.value());
            }

            AppendLog(async_mfc::NowTimeText() + _T(" [UI] 阶段B开始..."));
            async_mfc::AwaitCancellableProgress(
                m_uiQueue,
                token,
                [](const async_mfc::CancellationToken& token,
                   const std::function<void(int)>& reportProgress) -> CString {
                    long long valueB = 0;
                    for (int i = 1; i <= 50; ++i) {
                        async_mfc::ThrowIfCancellationRequested(token);
                        std::this_thread::sleep_for(std::chrono::milliseconds(45));
                        for (int j = 1; j <= 1200; ++j) {
                            valueB += (i * j) % 19;
                        }
                        reportProgress(50 + i);
                    }

                    CString stepB;
                    stepB.Format(_T("阶段B完成，valueB=%lld"), valueB);
                    return stepB;
                },
                [this](int percent) {
                    m_progress.SetPos(percent);
                },
                [this](std::optional<CString> resultB, std::exception_ptr epB) {
                    FinishWithResult(resultB, epB);
                });
        });
}

void CMainFrame::OnCancelButtonClicked() {
    if (!m_isRunning || !m_cancelSource.has_value()) {
        return;
    }

    m_cancelSource->Cancel();
    AppendLog(async_mfc::NowTimeText() + _T(" [UI] 已请求取消，等待后台线程退出..."));
    m_btnCancel.EnableWindow(FALSE);
}

LRESULT CMainFrame::OnAsyncContinuation(WPARAM, LPARAM) {
    if (m_uiQueue) {
        m_uiQueue->Drain();
    }
    return 0;
}

void CMainFrame::AppendLog(const CString& line) {
    int len = m_log.GetWindowTextLength();
    m_log.SetSel(len, len);
    m_log.ReplaceSel(line + _T("\r\n"));
}

void CMainFrame::SetRunningState(bool running) {
    m_isRunning = running;
    m_btnStart.EnableWindow(!running);
    m_btnChain.EnableWindow(!running);
    m_btnCancel.EnableWindow(running);
}

void CMainFrame::FinishWithResult(const std::optional<CString>& result, std::exception_ptr ep) {
    if (ep) {
        if (async_mfc::IsCanceledException(ep)) {
            AppendLog(async_mfc::NowTimeText() + _T(" [UI] 任务已取消"));
        } else {
            AppendLog(async_mfc::NowTimeText() + _T(" [UI] 任务失败"));
        }
    } else if (result.has_value()) {
        AppendLog(async_mfc::NowTimeText() + _T(" [UI] ") + result.value());
        AppendLog(async_mfc::NowTimeText() + _T(" [UI] 异步流程结束，界面始终保持响应。"));
    }

    m_cancelSource.reset();
    SetRunningState(false);
}

void CMainFrame::LayoutControls() {
    if (!::IsWindow(m_hWnd) || !::IsWindow(m_btnStart.m_hWnd) || !::IsWindow(m_log.m_hWnd)) {
        return;
    }

    CRect rc;
    GetClientRect(&rc);

    int dpi = static_cast<int>(GetCurrentDpi());
    int margin = MulDiv(12, dpi, 96);
    int gap = MulDiv(10, dpi, 96);
    int titleHeight = MulDiv(24, dpi, 96);
    int btnHeight = MulDiv(36, dpi, 96);
    int progressHeight = MulDiv(20, dpi, 96);

    int available = rc.Width() - margin * 2;
    if (available < MulDiv(300, dpi, 96)) {
        available = MulDiv(300, dpi, 96);
    }

    int rowY = margin + titleHeight + gap;

    int baseStart = MulDiv(130, dpi, 96);
    int baseChain = MulDiv(230, dpi, 96);
    int baseCancel = MulDiv(120, dpi, 96);
    int minProgress = MulDiv(130, dpi, 96);
    int fixed = baseStart + baseChain + baseCancel + gap * 3;

    int progressX = 0;
    int progressY = rowY + (btnHeight - progressHeight) / 2;
    int progressW = 0;
    int logTop = 0;

    if (available >= fixed + minProgress) {
        int x = margin;
        m_btnStart.MoveWindow(x, rowY, baseStart, btnHeight);
        x += baseStart + gap;

        m_btnChain.MoveWindow(x, rowY, baseChain, btnHeight);
        x += baseChain + gap;

        m_btnCancel.MoveWindow(x, rowY, baseCancel, btnHeight);
        x += baseCancel + gap;

        progressX = x;
        progressW = MaxInt(minProgress, rc.Width() - margin - progressX);
        logTop = rowY + btnHeight + gap;
    } else {
        int btnTotal = MaxInt(1, baseStart + baseChain + baseCancel);
        int btnAvail = MaxInt(3, available - gap * 2);

        int startW = MaxInt(MulDiv(90, dpi, 96), btnAvail * baseStart / btnTotal);
        int chainW = MaxInt(MulDiv(140, dpi, 96), btnAvail * baseChain / btnTotal);
        int cancelW = MaxInt(MulDiv(90, dpi, 96), btnAvail - startW - chainW);

        int x = margin;
        m_btnStart.MoveWindow(x, rowY, startW, btnHeight);
        x += startW + gap;
        m_btnChain.MoveWindow(x, rowY, chainW, btnHeight);
        x += chainW + gap;
        m_btnCancel.MoveWindow(x, rowY, cancelW, btnHeight);

        progressX = margin;
        progressY = rowY + btnHeight + gap;
        progressW = MaxInt(minProgress, available);
        logTop = progressY + progressHeight + gap;
    }

    m_progress.MoveWindow(progressX, progressY, progressW, progressHeight);

    int logHeight = MaxInt(MulDiv(180, dpi, 96), rc.Height() - logTop - margin);
    m_log.MoveWindow(margin, logTop, MaxInt(MulDiv(240, dpi, 96), available), logHeight);
}

UINT CMainFrame::GetCurrentDpi() const {
    if (!::IsWindow(m_hWnd)) {
        return 96;
    }

    UINT dpi = ::GetDpiForWindow(m_hWnd);
    if (dpi != 0) {
        return dpi;
    }

    HDC hdc = ::GetDC(m_hWnd);
    if (hdc == nullptr) {
        return 96;
    }

    int dpiX = ::GetDeviceCaps(hdc, LOGPIXELSX);
    ::ReleaseDC(m_hWnd, hdc);
    return dpiX > 0 ? static_cast<UINT>(dpiX) : 96;
}

int CMainFrame::ScaleByDpi(int value) const {
    return MulDiv(value, static_cast<int>(GetCurrentDpi()), 96);
}
