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

#include "CommandEventBus.h"
#include "UiEventBus.h"

namespace {
constexpr UINT WM_EB_UI_TASK = WM_APP + 100;
constexpr UINT_PTR kUiRefreshTimerId = 1;
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
const char* kTickCoalesceKey = "tick.ui.latest";
}

struct LoginEvent {
    std::string user;
};

struct TickEvent {
    int frame;
};

class CMainFrame : public CFrameWnd {
public:
    CMainFrame()
        : loginToken_(0), tickWorkerToken_(0), uiScenarioToken_(0), uiRefreshToken_(0),
                    cmdPublishLoginToken_(0), cmdSetPolicyToken_(0), cmdBurstToken_(0),
                    cmdToggleUiExecutorToken_(0), cmdResetCountersToken_(0), cmdPublishSyncTickToken_(0),
                    running_(false), loginEventCount_(0), workerTickCount_(0), lastUiFrame_(-1),
                    asyncRejectCount_(0), queueFullCount_(0), uiExecMissingCount_(0),
                    workerNotRunningCount_(0), noSubscriberCount_(0),
                    syncPublishCount_(0), syncPublishFailCount_(0),
                    burstRunning_(false),
                    shuttingDown_(false),
                    uiExecutorEnabled_(true),
                    lastLoginUser_("(none)"), lastPublishNote_("(none)"),
                    activePolicyName_("RejectNew"),
                    lastScenarioNote_("Producer thread uses coalesced publish."),
                    loginRuleOk_(false),
                    uiThreadId_(::GetCurrentThreadId()) {
        Create(NULL, _T("EventBus MFC Demo"), WS_OVERLAPPEDWINDOW, CRect(100, 100, 900, 500));
    }

    afx_msg int OnCreate(LPCREATESTRUCT lpCreateStruct) {
        if (CFrameWnd::OnCreate(lpCreateStruct) == -1) {
            return -1;
        }

        // Build a tiny visible UI so demo behavior is obvious.
        CRect clientRect;
        GetClientRect(&clientRect);

        statusText_.Create(
            _T("Starting EventBus demo..."),
            WS_CHILD | WS_VISIBLE | SS_LEFT,
            CRect(20, 180, clientRect.Width() - 20, clientRect.Height() - 20),
            this,
            kControlStatus);

        publishLoginButton_.Create(
            _T("Publish Login Event"),
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
            _T("Burst Raw Async (400)"),
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
            _T("Publish Sync Tick"),
            WS_CHILD | WS_VISIBLE | BS_PUSHBUTTON,
            CRect(20, 50, 200, 85),
            this,
            kControlPublishSyncTick);

        commandBus_.ConfigureAsyncQueue(
            256,
            eb::AsyncQueuePolicy::RejectNew);
        const bool workerStarted = commandBus_.StartAsyncWorker();

        uiBus_.BindUiDispatcher(GetSafeHwnd(), WM_EB_UI_TASK);

        loginToken_ = commandBus_.Subscribe<LoginEvent>(
            [this](const LoginEvent& e) {
                loginEventCount_.fetch_add(1);
                {
                    std::lock_guard<std::mutex> lock(loginStateMutex_);
                    lastLoginUser_ = e.user;
                }
                RequestUiRefresh();
            },
            eb::RegistrationRule::OneToOne);

        const std::uint64_t loginSecond = commandBus_.Subscribe<LoginEvent>(
            [](const LoginEvent& e) {
                UNREFERENCED_PARAMETER(e);
            },
            eb::RegistrationRule::OneToOne);

        loginRuleOk_ = (loginToken_ != 0 && loginSecond == 0);

        tickWorkerToken_ = commandBus_.Subscribe<TickEvent>(
            [this](const TickEvent& e) {
                workerTickCount_.fetch_add(1);
                lastUiFrame_.store(e.frame);
                RequestUiRefresh();
            },
            eb::RegistrationRule::OneToMany,
            eb::DispatchTarget::CurrentThread);

        uiScenarioToken_ = uiBus_.SubscribeUiScenarioNote(
            this,
            &CMainFrame::OnUiScenarioNote);

        uiRefreshToken_ = uiBus_.SubscribeUiRefresh(
            this,
            &CMainFrame::OnUiRefreshView);

        cmdPublishLoginToken_ = commandBus_.SubscribeCommandPublishLogin(
            this,
            &CMainFrame::OnCommandPublishLogin);

        cmdSetPolicyToken_ = commandBus_.SubscribeCommandSetPolicy(
            this,
            &CMainFrame::OnCommandSetPolicy);

        cmdBurstToken_ = commandBus_.SubscribeCommandRunBurst(
            this,
            &CMainFrame::OnCommandRunBurst);

        cmdToggleUiExecutorToken_ = commandBus_.SubscribeCommandToggleUiExecutor(
            this,
            &CMainFrame::OnCommandToggleUiExecutor);

        cmdResetCountersToken_ = commandBus_.SubscribeCommandResetCounters(
            this,
            &CMainFrame::OnCommandResetCounters);

        cmdPublishSyncTickToken_ = commandBus_.SubscribeCommandPublishSyncTick(
            this,
            &CMainFrame::OnCommandPublishSyncTick);

        const eb::PublishStatus initLoginStatus = commandBus_.PublishSync(LoginEvent{"alice"});
        if (initLoginStatus != eb::PublishStatus::Ok) {
            lastPublishNote_ = "Initial login publish failed";
        }

        if (!workerStarted) {
            std::lock_guard<std::mutex> lock(loginStateMutex_);
            lastScenarioNote_ = "Async worker failed to start.";
        }

        if (workerStarted) {
            running_.store(true);
            shuttingDown_.store(false);
            try {
                producer_ = std::thread([this]() {
                    struct RunningResetGuard {
                        explicit RunningResetGuard(std::atomic<bool>* f) : flag(f) {}
                        ~RunningResetGuard() {
                            if (flag != NULL) {
                                flag->store(false);
                            }
                        }
                        std::atomic<bool>* flag;
                    } guard(&running_);

                    int frame = 0;
                    try {
                        while (running_.load() && !shuttingDown_.load()) {
                            const eb::PublishStatus asyncStatus = commandBus_.PublishAsyncCoalesced(TickEvent{frame}, kTickCoalesceKey);
                            if (asyncStatus != eb::PublishStatus::Ok) {
                                RecordAsyncStatus(asyncStatus, "producer");
                            }
                            ++frame;
                            std::this_thread::sleep_for(std::chrono::milliseconds(300));
                        }
                    } catch (...) {
                        running_.store(false);
                        std::lock_guard<std::mutex> lock(loginStateMutex_);
                        lastScenarioNote_ = "Producer thread terminated unexpectedly.";
                    }
                });
            } catch (...) {
                running_.store(false);
                std::lock_guard<std::mutex> lock(loginStateMutex_);
                lastScenarioNote_ = "Failed to start producer thread.";
            }
        }

        SetTimer(kUiRefreshTimerId, 200, NULL);

        UpdateCaption();
        UpdateStatusText();
        return 0;
    }

    afx_msg void OnTimer(UINT_PTR nIDEvent) {
        if (shuttingDown_.load()) {
            CFrameWnd::OnTimer(nIDEvent);
            return;
        }

        if (nIDEvent == kUiRefreshTimerId) {
            UpdateStatusText();
        }
        CFrameWnd::OnTimer(nIDEvent);
    }

    afx_msg void OnPublishLoginClicked() {
        if (shuttingDown_.load()) {
            return;
        }
        PublishCommand(commandBus_.PublishCommandPublishLogin("button_click"), "button-login");
    }

    afx_msg void OnPolicyRejectClicked() {
        if (shuttingDown_.load()) {
            return;
        }
        PublishCommand(commandBus_.PublishCommandSetPolicy(eb::AsyncQueuePolicy::RejectNew, 256), "policy-reject");
    }

    afx_msg void OnPolicyWaitClicked() {
        if (shuttingDown_.load()) {
            return;
        }
        PublishCommand(commandBus_.PublishCommandSetPolicy(eb::AsyncQueuePolicy::WaitForSpace, 256), "policy-wait");
    }

    afx_msg void OnPolicyDropOldestClicked() {
        if (shuttingDown_.load()) {
            return;
        }
        PublishCommand(commandBus_.PublishCommandSetPolicy(eb::AsyncQueuePolicy::DropOldest, 256), "policy-drop-oldest");
    }

    afx_msg void OnBurstRawClicked() {
        if (shuttingDown_.load()) {
            return;
        }
        PublishCommand(commandBus_.PublishCommandRunBurst(false, 400), "burst-raw-cmd");
    }

    afx_msg void OnBurstCoalescedClicked() {
        if (shuttingDown_.load()) {
            return;
        }
        PublishCommand(commandBus_.PublishCommandRunBurst(true, 400), "burst-coalesced-cmd");
    }

    afx_msg void OnToggleUiExecutorClicked() {
        if (shuttingDown_.load()) {
            return;
        }
        PublishCommand(commandBus_.PublishCommandToggleUiExecutor(), "toggle-ui-executor");
    }

    afx_msg void OnResetCountersClicked() {
        if (shuttingDown_.load()) {
            return;
        }
        PublishCommand(commandBus_.PublishCommandResetCounters(), "reset-counters");
    }

    afx_msg void OnPublishSyncTickClicked() {
        if (shuttingDown_.load()) {
            return;
        }
        PublishCommand(commandBus_.PublishCommandPublishSyncTick(), "sync-tick-cmd");
    }

    afx_msg LRESULT OnEventBusUiTask(WPARAM wParam, LPARAM lParam) {
        UNREFERENCED_PARAMETER(wParam);
        if (shuttingDown_.load()) {
            return 0;
        }
        uiBus_.HandleUiTaskMessage(lParam);
        return 0;
    }

    afx_msg void OnDestroy() {
        KillTimer(kUiRefreshTimerId);

        if (shuttingDown_.exchange(true)) {
            CFrameWnd::OnDestroy();
            return;
        }

        running_.store(false);

        publishLoginButton_.EnableWindow(FALSE);
        policyRejectButton_.EnableWindow(FALSE);
        policyWaitButton_.EnableWindow(FALSE);
        policyDropOldestButton_.EnableWindow(FALSE);
        burstRawButton_.EnableWindow(FALSE);
        burstCoalescedButton_.EnableWindow(FALSE);
        toggleUiExecutorButton_.EnableWindow(FALSE);
        resetCountersButton_.EnableWindow(FALSE);
        publishSyncTickButton_.EnableWindow(FALSE);

        // Stop worker first so waiting publishers can return and exit cleanly.
        commandBus_.StopAsyncWorker();

        if (producer_.joinable()) {
            producer_.join();
        }
        if (burstThread_.joinable()) {
            burstThread_.join();
        }

        if (loginToken_ != 0) {
            commandBus_.Unsubscribe(loginToken_);
            loginToken_ = 0;
        }
        if (tickWorkerToken_ != 0) {
            commandBus_.Unsubscribe(tickWorkerToken_);
            tickWorkerToken_ = 0;
        }
        if (uiScenarioToken_ != 0) {
            uiBus_.Unsubscribe(uiScenarioToken_);
            uiScenarioToken_ = 0;
        }
        if (uiRefreshToken_ != 0) {
            uiBus_.Unsubscribe(uiRefreshToken_);
            uiRefreshToken_ = 0;
        }
        if (cmdPublishLoginToken_ != 0) {
            commandBus_.Unsubscribe(cmdPublishLoginToken_);
            cmdPublishLoginToken_ = 0;
        }
        if (cmdSetPolicyToken_ != 0) {
            commandBus_.Unsubscribe(cmdSetPolicyToken_);
            cmdSetPolicyToken_ = 0;
        }
        if (cmdBurstToken_ != 0) {
            commandBus_.Unsubscribe(cmdBurstToken_);
            cmdBurstToken_ = 0;
        }
        if (cmdToggleUiExecutorToken_ != 0) {
            commandBus_.Unsubscribe(cmdToggleUiExecutorToken_);
            cmdToggleUiExecutorToken_ = 0;
        }
        if (cmdResetCountersToken_ != 0) {
            commandBus_.Unsubscribe(cmdResetCountersToken_);
            cmdResetCountersToken_ = 0;
        }
        if (cmdPublishSyncTickToken_ != 0) {
            commandBus_.Unsubscribe(cmdPublishSyncTickToken_);
            cmdPublishSyncTickToken_ = 0;
        }

        uiBus_.UnbindUiDispatcher();
        uiBus_.DisposePendingUiTasks();

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

                int failureCount = 0;
                bool internalError = false;
                try {
                    for (int i = 0; i < runCount && !shuttingDown_.load(); ++i) {
                        eb::PublishStatus st = eb::PublishStatus::Ok;
                        if (coalesced) {
                            st = commandBus_.PublishAsyncCoalesced(TickEvent{200000 + i}, kTickCoalesceKey);
                        } else {
                            st = commandBus_.PublishAsync(TickEvent{100000 + i});
                        }

                        if (st != eb::PublishStatus::Ok) {
                            ++failureCount;
                            RecordAsyncStatus(st, coalesced ? "burst-coalesced" : "burst-raw");
                        }
                    }
                } catch (...) {
                    internalError = true;
                    std::lock_guard<std::mutex> lock(loginStateMutex_);
                    lastScenarioNote_ = "Burst worker terminated unexpectedly.";
                }

                if (!internalError && !shuttingDown_.load()) {
                    std::lock_guard<std::mutex> lock(loginStateMutex_);
                    lastScenarioNote_ = std::string(coalesced ? "Burst Coalesced done. failures=" : "Burst Raw done. failures=") + std::to_string(failureCount);
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

    void OnCommandPublishLogin(const eb::CommandPublishLoginEvent& e) {
        const eb::PublishStatus st = commandBus_.PublishSync(LoginEvent{e.user});
        {
            std::lock_guard<std::mutex> lock(loginStateMutex_);
            if (st == eb::PublishStatus::Ok) {
                lastPublishNote_ = "Login publish OK";
            } else if (st == eb::PublishStatus::NoSubscribers) {
                lastPublishNote_ = "Login publish failed: NoSubscribers";
            } else if (st == eb::PublishStatus::CallbackException) {
                lastPublishNote_ = "Login publish failed: CallbackException";
            } else {
                lastPublishNote_ = "Login publish failed";
            }
        }
        RequestUiRefresh();
    }

    void OnCommandSetPolicy(const eb::CommandSetPolicyEvent& e) {
        commandBus_.ConfigureAsyncQueue(e.queueSize, e.policy);
        {
            std::lock_guard<std::mutex> lock(loginStateMutex_);
            if (e.policy == eb::AsyncQueuePolicy::RejectNew) {
                activePolicyName_ = "RejectNew";
            } else if (e.policy == eb::AsyncQueuePolicy::WaitForSpace) {
                activePolicyName_ = "WaitForSpace";
            } else {
                activePolicyName_ = "DropOldest";
            }
        }
        PublishScenarioNote(std::string("Policy switched to ") + activePolicyName_ + ".");
        RequestUiRefresh();
    }

    void OnCommandRunBurst(const eb::CommandRunBurstEvent& e) {
        StartBurstScenario(e.coalesced, e.count);
    }

    void OnCommandToggleUiExecutor(const eb::CommandToggleUiExecutorEvent&) {
        if (uiExecutorEnabled_) {
            uiBus_.UnbindUiDispatcher();
            uiExecutorEnabled_ = false;
            std::lock_guard<std::mutex> lock(loginStateMutex_);
            lastScenarioNote_ = "UiExecutor disabled.";
        } else {
            uiBus_.BindUiDispatcher(GetSafeHwnd(), WM_EB_UI_TASK);
            uiExecutorEnabled_ = true;
            std::lock_guard<std::mutex> lock(loginStateMutex_);
            lastScenarioNote_ = "UiExecutor enabled.";
        }
        RequestUiRefresh();
    }

    void OnCommandResetCounters(const eb::CommandResetCountersEvent&) {
        asyncRejectCount_.store(0);
        queueFullCount_.store(0);
        uiExecMissingCount_.store(0);
        workerNotRunningCount_.store(0);
        noSubscriberCount_.store(0);
        syncPublishCount_.store(0);
        syncPublishFailCount_.store(0);
        commandBus_.ResetAsyncStats();
        uiBus_.ResetUiTaskStats();
        PublishScenarioNote("Counters reset.");
        RequestUiRefresh();
    }

    void OnCommandPublishSyncTick(const eb::CommandPublishSyncTickEvent&) {
        const int frame = 300000 + syncPublishCount_.load();
        const eb::PublishStatus st = commandBus_.PublishSync(TickEvent{frame});
        if (st == eb::PublishStatus::Ok) {
            syncPublishCount_.fetch_add(1);
            std::lock_guard<std::mutex> lock(loginStateMutex_);
            lastScenarioNote_ = std::string("Sync Tick published. frame=") + std::to_string(frame);
        } else {
            syncPublishFailCount_.fetch_add(1);
            RecordAsyncStatus(st, "sync-publish");
        }
        RequestUiRefresh();
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

    void PublishCommand(eb::PublishStatus st, const char* source) {
        if (st != eb::PublishStatus::Ok) {
            RecordAsyncStatus(st, source);
        }
        RequestUiRefresh();
    }

    void RecordAsyncStatus(eb::PublishStatus st, const char* source) {
        switch (st) {
            case eb::PublishStatus::QueueFull:
                queueFullCount_.fetch_add(1);
                asyncRejectCount_.fetch_add(1);
                break;
            case eb::PublishStatus::UiExecutorNotConfigured:
                uiExecMissingCount_.fetch_add(1);
                asyncRejectCount_.fetch_add(1);
                break;
            case eb::PublishStatus::WorkerNotRunning:
                workerNotRunningCount_.fetch_add(1);
                asyncRejectCount_.fetch_add(1);
                break;
            case eb::PublishStatus::NoSubscribers:
                noSubscriberCount_.fetch_add(1);
                asyncRejectCount_.fetch_add(1);
                break;
            default:
                asyncRejectCount_.fetch_add(1);
                break;
        }

        std::lock_guard<std::mutex> lock(loginStateMutex_);
        lastScenarioNote_ = std::string(source) + ": status=" + std::to_string(static_cast<int>(st));
    }

    void UpdateStatusText() {
        const eb::EventBus::AsyncRuntimeStats asyncStats = commandBus_.GetAsyncRuntimeStats();
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
            _T("Login one-to-one: %s\r\nLogin events handled: %d\r\nLast login user: %s\r\nWorker tick callbacks: %d\r\nLast UI frame: %d\r\nPolicy: %s\r\nUiExecutor enabled: %s\r\nWorker running: %s\r\nStop in progress: %s\r\nPending async tasks: %d\r\nPending UI tasks: %d\r\nSync publishes: %d\r\nSync publish fails: %d\r\nDropped async tasks: %d\r\nDropped UI tasks: %d\r\nRejected async publishes: %d\r\nQueueFull count: %d\r\nUiExecutor missing count: %d\r\nWorkerNotRunning count: %d\r\nNoSubscribers count: %d\r\nTick coalesce key: %S\r\nLast publish note: %s\r\nLast scenario note: %s"),
            loginRuleOk_ ? _T("OK") : _T("FAIL"),
            loginEventCount_.load(),
            loginUser.GetString(),
            workerTickCount_.load(),
            lastUiFrame_.load(),
            policyName.GetString(),
            uiExecutorEnabled_ ? _T("YES") : _T("NO"),
            asyncStats.workerRunning ? _T("YES") : _T("NO"),
            asyncStats.stopInProgress ? _T("YES") : _T("NO"),
            static_cast<int>(asyncStats.pendingTaskCount),
            static_cast<int>(uiStats.pendingTaskCount),
            syncPublishCount_.load(),
            syncPublishFailCount_.load(),
            static_cast<int>(asyncStats.droppedTaskCount),
            static_cast<int>(uiStats.droppedTaskCount),
            asyncRejectCount_.load(),
            queueFullCount_.load(),
            uiExecMissingCount_.load(),
            workerNotRunningCount_.load(),
            noSubscriberCount_.load(),
            kTickCoalesceKey,
            publishNote.GetString(),
            scenarioNote.GetString());
        statusText_.SetWindowText(status);
    }

    void UpdateCaption() {
        CString title;
        title.Format(
            _T("EventBus MFC Demo | LoginRule=%s | Logins=%d | WorkerTicks=%d | LastUiFrame=%d | Dropped=%d"),
            loginRuleOk_ ? _T("OK") : _T("FAIL"),
            loginEventCount_.load(),
            workerTickCount_.load(),
            lastUiFrame_.load(),
            static_cast<int>(commandBus_.DroppedAsyncTaskCount()));
        SetWindowText(title);
    }

private:
    eb::UiEventBus uiBus_;
    eb::CommandEventBus commandBus_;
    std::uint64_t loginToken_;
    std::uint64_t tickWorkerToken_;
    std::uint64_t uiScenarioToken_;
    std::uint64_t uiRefreshToken_;
    std::uint64_t cmdPublishLoginToken_;
    std::uint64_t cmdSetPolicyToken_;
    std::uint64_t cmdBurstToken_;
    std::uint64_t cmdToggleUiExecutorToken_;
    std::uint64_t cmdResetCountersToken_;
    std::uint64_t cmdPublishSyncTickToken_;
    std::atomic<bool> running_;
    std::thread producer_;
    std::atomic<int> loginEventCount_;
    std::atomic<int> workerTickCount_;
    std::atomic<int> lastUiFrame_;
    std::atomic<int> asyncRejectCount_;
    std::atomic<int> queueFullCount_;
    std::atomic<int> uiExecMissingCount_;
    std::atomic<int> workerNotRunningCount_;
    std::atomic<int> noSubscriberCount_;
    std::atomic<int> syncPublishCount_;
    std::atomic<int> syncPublishFailCount_;
    std::atomic<bool> burstRunning_;
    std::atomic<bool> shuttingDown_;
    std::mutex loginStateMutex_;
    std::string lastLoginUser_;
    std::string lastPublishNote_;
    std::string activePolicyName_;
    std::string lastScenarioNote_;
    bool uiExecutorEnabled_;
    bool loginRuleOk_;
    DWORD uiThreadId_;
    std::thread burstThread_;
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

    DECLARE_MESSAGE_MAP()
};

BEGIN_MESSAGE_MAP(CMainFrame, CFrameWnd)
    ON_WM_CREATE()
    ON_WM_TIMER()
    ON_BN_CLICKED(kControlPublishLogin, &CMainFrame::OnPublishLoginClicked)
    ON_BN_CLICKED(kControlPolicyReject, &CMainFrame::OnPolicyRejectClicked)
    ON_BN_CLICKED(kControlPolicyWait, &CMainFrame::OnPolicyWaitClicked)
    ON_BN_CLICKED(kControlPolicyDropOldest, &CMainFrame::OnPolicyDropOldestClicked)
    ON_BN_CLICKED(kControlBurstRaw, &CMainFrame::OnBurstRawClicked)
    ON_BN_CLICKED(kControlBurstCoalesced, &CMainFrame::OnBurstCoalescedClicked)
    ON_BN_CLICKED(kControlToggleUiExecutor, &CMainFrame::OnToggleUiExecutorClicked)
    ON_BN_CLICKED(kControlResetCounters, &CMainFrame::OnResetCountersClicked)
    ON_BN_CLICKED(kControlPublishSyncTick, &CMainFrame::OnPublishSyncTickClicked)
    ON_MESSAGE(WM_EB_UI_TASK, &CMainFrame::OnEventBusUiTask)
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
