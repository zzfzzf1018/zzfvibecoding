#ifndef _WIN32_WINNT
#define _WIN32_WINNT 0x0601
#endif

#include <afxwin.h>

#include <atomic>
#include <chrono>
#include <functional>
#include <memory>
#include <mutex>
#include <string>
#include <thread>
#include <utility>

#include "UiEventBus.h"

namespace {
constexpr int kControlStatus = 1001;
constexpr int kControlPublishLogin = 1002;
constexpr int kControlPolicyReject = 1003;
constexpr int kControlPolicyWait = 1004;
constexpr int kControlPolicyDropOldest = 1005;
constexpr int kControlBurstRaw = 1006;
constexpr int kControlBurstCoalesced = 1007;
constexpr int kControlToggleUiExecutor = 1008;
constexpr int kControlResetCounters = 1009;
constexpr int kControlPublishSyncTick = 1010;
constexpr int kControlThreadedUpdateTest = 1011;
}

class CMainFrame : public CFrameWnd {
public:
    CMainFrame()
        : uiScenarioToken_(0), uiRefreshToken_(0),
                    loginEventCount_(0), workerTickCount_(0), lastUiFrame_(-1),
                    syncPublishCount_(0),
                    burstRunning_(false),
                    threadedUpdateRunning_(false),
                    shuttingDown_(false),
                    uiExecutorEnabled_(true),
                    lastLoginUser_("(none)"), lastPublishNote_("(none)"),
                    activePolicyName_("RejectNew"),
                    lastScenarioNote_("Manual actions update UI directly."),
                    uiThreadId_(::GetCurrentThreadId()) {
        Create(NULL, _T("Direct UI Demo"), WS_OVERLAPPEDWINDOW, CRect(100, 100, 900, 500));
    }

    afx_msg int OnCreate(LPCREATESTRUCT lpCreateStruct) {
        if (CFrameWnd::OnCreate(lpCreateStruct) == -1) {
            return -1;
        }

        // Build a tiny visible UI so demo behavior is obvious.
        CRect clientRect;
        GetClientRect(&clientRect);

        statusText_.Create(
            _T("Starting direct handling demo..."),
            WS_CHILD | WS_VISIBLE | SS_LEFT,
            CRect(20, 180, clientRect.Width() - 20, clientRect.Height() - 20),
            this,
            kControlStatus);

        publishLoginButton_.Create(
            _T("Handle Login"),
            WS_CHILD | WS_VISIBLE | BS_PUSHBUTTON,
            CRect(20, 90, 200, 125),
            this,
            kControlPublishLogin);

        policyRejectButton_.Create(
            _T("Policy: RejectNew"),
            WS_CHILD | WS_VISIBLE | BS_PUSHBUTTON,
            CRect(220, 90, 390, 125),
            this,
            kControlPolicyReject);

        policyWaitButton_.Create(
            _T("Policy: WaitForSpace"),
            WS_CHILD | WS_VISIBLE | BS_PUSHBUTTON,
            CRect(400, 90, 580, 125),
            this,
            kControlPolicyWait);

        policyDropOldestButton_.Create(
            _T("Policy: DropOldest"),
            WS_CHILD | WS_VISIBLE | BS_PUSHBUTTON,
            CRect(590, 90, 770, 125),
            this,
            kControlPolicyDropOldest);

        burstRawButton_.Create(
            _T("Burst Raw (400)"),
            WS_CHILD | WS_VISIBLE | BS_PUSHBUTTON,
            CRect(20, 130, 240, 165),
            this,
            kControlBurstRaw);

        burstCoalescedButton_.Create(
            _T("Burst Coalesced (400)"),
            WS_CHILD | WS_VISIBLE | BS_PUSHBUTTON,
            CRect(250, 130, 480, 165),
            this,
            kControlBurstCoalesced);

        toggleUiExecutorButton_.Create(
            _T("Toggle UiExecutor"),
            WS_CHILD | WS_VISIBLE | BS_PUSHBUTTON,
            CRect(490, 130, 680, 165),
            this,
            kControlToggleUiExecutor);

        resetCountersButton_.Create(
            _T("Reset Counters"),
            WS_CHILD | WS_VISIBLE | BS_PUSHBUTTON,
            CRect(690, 130, 860, 165),
            this,
            kControlResetCounters);

        publishSyncTickButton_.Create(
            _T("Handle Sync Tick"),
            WS_CHILD | WS_VISIBLE | BS_PUSHBUTTON,
            CRect(20, 50, 200, 85),
            this,
            kControlPublishSyncTick);

        threadedUpdateButton_.Create(
            _T("Threaded Update Test"),
            WS_CHILD | WS_VISIBLE | BS_PUSHBUTTON,
            CRect(220, 50, 430, 85),
            this,
            kControlThreadedUpdateTest);

        uiBus_.EnableUiExecutor();

        uiScenarioToken_ = uiBus_.SubscribeUiScenarioNote(
            this,
            &CMainFrame::OnUiScenarioNote);

        uiRefreshToken_ = uiBus_.SubscribeUiRefresh(
            this,
            &CMainFrame::OnUiRefreshView);

        shuttingDown_.store(false);

        UpdateCaption();
        UpdateStatusText();
        return 0;
    }

    afx_msg void OnPublishLoginClicked() {
        if (shuttingDown_.load()) {
            return;
        }
        HandlePublishLogin("button_click");
    }

    afx_msg void OnPolicyRejectClicked() {
        if (shuttingDown_.load()) {
            return;
        }
        HandleSetPolicy(eb::AsyncQueuePolicy::RejectNew, 256);
    }

    afx_msg void OnPolicyWaitClicked() {
        if (shuttingDown_.load()) {
            return;
        }
        HandleSetPolicy(eb::AsyncQueuePolicy::WaitForSpace, 256);
    }

    afx_msg void OnPolicyDropOldestClicked() {
        if (shuttingDown_.load()) {
            return;
        }
        HandleSetPolicy(eb::AsyncQueuePolicy::DropOldest, 256);
    }

    afx_msg void OnBurstRawClicked() {
        if (shuttingDown_.load()) {
            return;
        }
        HandleRunBurst(false, 400);
    }

    afx_msg void OnBurstCoalescedClicked() {
        if (shuttingDown_.load()) {
            return;
        }
        HandleRunBurst(true, 400);
    }

    afx_msg void OnToggleUiExecutorClicked() {
        if (shuttingDown_.load()) {
            return;
        }
        HandleToggleUiExecutor();
    }

    afx_msg void OnResetCountersClicked() {
        if (shuttingDown_.load()) {
            return;
        }
        HandleResetCounters();
    }

    afx_msg void OnPublishSyncTickClicked() {
        if (shuttingDown_.load()) {
            return;
        }
        HandlePublishSyncTick();
    }

    afx_msg void OnThreadedUpdateTestClicked() {
        if (shuttingDown_.load()) {
            return;
        }
        HandleThreadedUpdateTest();
    }

    afx_msg void OnDestroy() {
        if (shuttingDown_.exchange(true)) {
            CFrameWnd::OnDestroy();
            return;
        }

        publishLoginButton_.EnableWindow(FALSE);
        policyRejectButton_.EnableWindow(FALSE);
        policyWaitButton_.EnableWindow(FALSE);
        policyDropOldestButton_.EnableWindow(FALSE);
        burstRawButton_.EnableWindow(FALSE);
        burstCoalescedButton_.EnableWindow(FALSE);
        toggleUiExecutorButton_.EnableWindow(FALSE);
        resetCountersButton_.EnableWindow(FALSE);
        publishSyncTickButton_.EnableWindow(FALSE);
        threadedUpdateButton_.EnableWindow(FALSE);

        if (burstThread_.joinable()) {
            burstThread_.join();
        }
        if (threadedUpdateThread_.joinable()) {
            threadedUpdateThread_.join();
        }
        if (uiScenarioToken_ != 0) {
            uiBus_.Unsubscribe(uiScenarioToken_);
            uiScenarioToken_ = 0;
        }
        if (uiRefreshToken_ != 0) {
            uiBus_.Unsubscribe(uiRefreshToken_);
            uiRefreshToken_ = 0;
        }

        CFrameWnd::OnDestroy();
    }

private:
    void StartBurstScenario(bool coalesced, int count) {
        if (shuttingDown_.load()) {
            return;
        }

        if (burstRunning_.exchange(true)) {
            {
                std::lock_guard<std::mutex> lock(loginStateMutex_);
                lastScenarioNote_ = "Burst already running...";
            }
            UpdateStatusText();
            return;
        }

        if (burstThread_.joinable()) {
            burstThread_.join();
        }

        {
            std::lock_guard<std::mutex> lock(loginStateMutex_);
            lastScenarioNote_ = coalesced ? "Running Burst Coalesced..." : "Running Burst Raw...";
        }
        UpdateStatusText();

        const int runCount = count <= 0 ? 1 : count;
        try {
            burstThread_ = std::thread([this, coalesced, runCount]() {
                struct BurstRunningResetGuard {
                    explicit BurstRunningResetGuard(std::atomic<bool>* f) : flag(f) {}
                    ~BurstRunningResetGuard() {
                        if (flag != NULL) {
                            flag->store(false);
                        }
                    }
                    std::atomic<bool>* flag;
                } guard(&burstRunning_);

                bool internalError = false;
                try {
                    for (int i = 0; i < runCount && !shuttingDown_.load(); ++i) {
                        workerTickCount_.fetch_add(1);
                        lastUiFrame_.store(100000 + i);
                    }
                } catch (...) {
                    internalError = true;
                    std::lock_guard<std::mutex> lock(loginStateMutex_);
                    lastScenarioNote_ = "Burst worker terminated unexpectedly.";
                }

                if (!internalError && !shuttingDown_.load()) {
                    std::lock_guard<std::mutex> lock(loginStateMutex_);
                    lastScenarioNote_ = std::string(coalesced ? "Burst Coalesced done." : "Burst Raw done.");
                }
                RequestUiRefresh();
            });
        } catch (...) {
            {
                std::lock_guard<std::mutex> lock(loginStateMutex_);
                lastScenarioNote_ = "Failed to start burst thread.";
            }
            burstRunning_.store(false);
            RequestUiRefresh();
        }
    }

    void HandlePublishLogin(const std::string& user) {
        loginEventCount_.fetch_add(1);
        {
            std::lock_guard<std::mutex> lock(loginStateMutex_);
            lastLoginUser_ = user;
            lastPublishNote_ = "Login handled directly";
        }
        RequestUiRefresh();
    }

    void HandleSetPolicy(eb::AsyncQueuePolicy policy, std::size_t queueSize) {
        UNREFERENCED_PARAMETER(queueSize);
        {
            std::lock_guard<std::mutex> lock(loginStateMutex_);
            if (policy == eb::AsyncQueuePolicy::RejectNew) {
                activePolicyName_ = "RejectNew";
            } else if (policy == eb::AsyncQueuePolicy::WaitForSpace) {
                activePolicyName_ = "WaitForSpace";
            } else {
                activePolicyName_ = "DropOldest";
            }
        }
        PublishScenarioNote(std::string("Policy switched to ") + activePolicyName_ + ".");
        RequestUiRefresh();
    }

    void HandleRunBurst(bool coalesced, int count) {
        StartBurstScenario(coalesced, count);
    }

    void HandleToggleUiExecutor() {
        if (uiExecutorEnabled_) {
            uiBus_.DisableUiExecutor();
            uiExecutorEnabled_ = false;
            std::lock_guard<std::mutex> lock(loginStateMutex_);
            lastScenarioNote_ = "UiExecutor disabled.";
        } else {
            uiBus_.EnableUiExecutor();
            uiExecutorEnabled_ = true;
            std::lock_guard<std::mutex> lock(loginStateMutex_);
            lastScenarioNote_ = "UiExecutor enabled.";
        }
        RequestUiRefresh();
    }

    void HandleResetCounters() {
        syncPublishCount_.store(0);
        uiBus_.ResetUiTaskStats();
        PublishScenarioNote("Counters reset.");
        RequestUiRefresh();
    }

    void HandlePublishSyncTick() {
        const int frame = 300000 + syncPublishCount_.load();
        syncPublishCount_.fetch_add(1);
        workerTickCount_.fetch_add(1);
        lastUiFrame_.store(frame);
        {
            std::lock_guard<std::mutex> lock(loginStateMutex_);
            lastScenarioNote_ = std::string("Sync Tick handled directly. frame=") + std::to_string(frame);
        }
        RequestUiRefresh();
    }

    void HandleThreadedUpdateTest() {
        if (threadedUpdateRunning_.exchange(true)) {
            {
                std::lock_guard<std::mutex> lock(loginStateMutex_);
                lastScenarioNote_ = "Threaded test is already running.";
            }
            RequestUiRefresh();
            return;
        }

        if (threadedUpdateThread_.joinable()) {
            threadedUpdateThread_.join();
        }

        try {
            threadedUpdateThread_ = std::thread([this]() {
                struct RunningResetGuard {
                    explicit RunningResetGuard(std::atomic<bool>* f) : flag(f) {}
                    ~RunningResetGuard() {
                        if (flag != NULL) {
                            flag->store(false);
                        }
                    }
                    std::atomic<bool>* flag;
                } guard(&threadedUpdateRunning_);

                std::this_thread::sleep_for(std::chrono::milliseconds(150));
                if (shuttingDown_.load()) {
                    return;
                }

                const int frame = 500000 + syncPublishCount_.load();
                workerTickCount_.fetch_add(1);
                lastUiFrame_.store(frame);

                {
                    std::lock_guard<std::mutex> lock(loginStateMutex_);
                    lastPublishNote_ = "Thread updated state";
                    lastScenarioNote_ = "Worker thread requested UiRefresh via event bus.";
                }

                RequestUiRefresh();
            });
        } catch (...) {
            threadedUpdateRunning_.store(false);
            {
                std::lock_guard<std::mutex> lock(loginStateMutex_);
                lastScenarioNote_ = "Failed to start threaded update test.";
            }
            RequestUiRefresh();
        }
    }

    void OnUiScenarioNote(const eb::UiScenarioNoteEvent& e) {
        std::lock_guard<std::mutex> lock(loginStateMutex_);
        lastScenarioNote_ = e.text;
    }

    void OnUiRefreshView(const eb::UiRefreshViewEvent&) {
        UpdateCaption();
        UpdateStatusText();
    }

    void PublishScenarioNote(const std::string& text) {
        const eb::PublishStatus st = uiBus_.PublishUiScenarioNote(text);
        if (st != eb::PublishStatus::Ok) {
            std::lock_guard<std::mutex> lock(loginStateMutex_);
            lastScenarioNote_ = text;
        }
    }

    void RequestUiRefresh() {
        if (shuttingDown_.load()) {
            return;
        }

        const eb::PublishStatus st = uiBus_.PublishUiRefresh();
        if (st != eb::PublishStatus::Ok) {
            if (::GetCurrentThreadId() != uiThreadId_) {
                return;
            }
            UpdateCaption();
            UpdateStatusText();
        }
    }

    void UpdateStatusText() {
        const eb::UiEventBus::UiTaskRuntimeStats uiStats = uiBus_.GetUiTaskRuntimeStats();

        CString loginUser;
        CString publishNote;
        CString policyName;
        CString scenarioNote;
        {
            std::lock_guard<std::mutex> lock(loginStateMutex_);
            loginUser = CString(lastLoginUser_.c_str());
            publishNote = CString(lastPublishNote_.c_str());
            policyName = CString(activePolicyName_.c_str());
            scenarioNote = CString(lastScenarioNote_.c_str());
        }

        CString status;
        status.Format(
            _T("Mode: Direct handling\r\nLogin events handled: %d\r\nLast login user: %s\r\nFrame updates: %d\r\nLast UI frame: %d\r\nPolicy: %s\r\nUiExecutor enabled: %s\r\nShutting down: %s\r\nPending UI tasks: %d\r\nSync tick count: %d\r\nDropped UI tasks: %d\r\nLast action: %s\r\nScenario: %s"),
            loginEventCount_.load(),
            loginUser.GetString(),
            workerTickCount_.load(),
            lastUiFrame_.load(),
            policyName.GetString(),
            uiExecutorEnabled_ ? _T("YES") : _T("NO"),
            shuttingDown_.load() ? _T("YES") : _T("NO"),
            static_cast<int>(uiStats.pendingTaskCount),
            syncPublishCount_.load(),
            static_cast<int>(uiStats.droppedTaskCount),
            publishNote.GetString(),
            scenarioNote.GetString());
        statusText_.SetWindowText(status);
    }

    void UpdateCaption() {
        CString title;
        title.Format(
            _T("Direct UI Demo | Logins=%d | FrameUpdates=%d | LastUiFrame=%d | DroppedUiTasks=%d"),
            loginEventCount_.load(),
            workerTickCount_.load(),
            lastUiFrame_.load(),
            static_cast<int>(uiBus_.DroppedUiTaskCount()));
        SetWindowText(title);
    }

private:
    eb::UiEventBus uiBus_;
    std::uint64_t uiScenarioToken_;
    std::uint64_t uiRefreshToken_;
    std::atomic<int> loginEventCount_;
    std::atomic<int> workerTickCount_;
    std::atomic<int> lastUiFrame_;
    std::atomic<int> syncPublishCount_;
    std::atomic<bool> burstRunning_;
    std::atomic<bool> threadedUpdateRunning_;
    std::atomic<bool> shuttingDown_;
    std::mutex loginStateMutex_;
    std::string lastLoginUser_;
    std::string lastPublishNote_;
    std::string activePolicyName_;
    std::string lastScenarioNote_;
    bool uiExecutorEnabled_;
    DWORD uiThreadId_;
    std::thread burstThread_;
    std::thread threadedUpdateThread_;
    CStatic statusText_;
    CButton publishLoginButton_;
    CButton policyRejectButton_;
    CButton policyWaitButton_;
    CButton policyDropOldestButton_;
    CButton burstRawButton_;
    CButton burstCoalescedButton_;
    CButton toggleUiExecutorButton_;
    CButton resetCountersButton_;
    CButton publishSyncTickButton_;
    CButton threadedUpdateButton_;

    DECLARE_MESSAGE_MAP()
};

BEGIN_MESSAGE_MAP(CMainFrame, CFrameWnd)
    ON_WM_CREATE()
    ON_BN_CLICKED(kControlPublishLogin, &CMainFrame::OnPublishLoginClicked)
    ON_BN_CLICKED(kControlPolicyReject, &CMainFrame::OnPolicyRejectClicked)
    ON_BN_CLICKED(kControlPolicyWait, &CMainFrame::OnPolicyWaitClicked)
    ON_BN_CLICKED(kControlPolicyDropOldest, &CMainFrame::OnPolicyDropOldestClicked)
    ON_BN_CLICKED(kControlBurstRaw, &CMainFrame::OnBurstRawClicked)
    ON_BN_CLICKED(kControlBurstCoalesced, &CMainFrame::OnBurstCoalescedClicked)
    ON_BN_CLICKED(kControlToggleUiExecutor, &CMainFrame::OnToggleUiExecutorClicked)
    ON_BN_CLICKED(kControlResetCounters, &CMainFrame::OnResetCountersClicked)
    ON_BN_CLICKED(kControlPublishSyncTick, &CMainFrame::OnPublishSyncTickClicked)
    ON_BN_CLICKED(kControlThreadedUpdateTest, &CMainFrame::OnThreadedUpdateTestClicked)
    ON_WM_DESTROY()
END_MESSAGE_MAP()

class CEventBusMfcApp : public CWinApp {
public:
    virtual BOOL InitInstance() {
        CWinApp::InitInstance();

        CMainFrame* pFrame = new CMainFrame();
        m_pMainWnd = pFrame;
        pFrame->ShowWindow(SW_SHOW);
        pFrame->UpdateWindow();
        return TRUE;
    }
};

CEventBusMfcApp theApp;

