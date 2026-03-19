#include <afxwin.h>

#include <atomic>
#include <chrono>
#include <functional>
#include <memory>
#include <mutex>
#include <string>
#include <thread>
#include <utility>

#include "EventBus.h"

namespace {
constexpr UINT WM_EB_UI_TASK = WM_APP + 100;
constexpr UINT_PTR kUiRefreshTimerId = 1;
constexpr int kControlStatus = 1001;
constexpr int kControlPublishLogin = 1002;
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
        : loginToken_(0), tickWorkerToken_(0), tickUiToken_(0),
                    running_(false), loginEventCount_(0), workerTickCount_(0), lastUiFrame_(-1),
                    lastLoginUser_("(none)"), loginRuleOk_(false) {
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
            CRect(20, 20, clientRect.Width() - 20, 80),
            this,
            kControlStatus);

        publishLoginButton_.Create(
            _T("Publish Login Event"),
            WS_CHILD | WS_VISIBLE | BS_PUSHBUTTON,
            CRect(20, 90, 200, 125),
            this,
            kControlPublishLogin);

        bus_.StartAsyncWorker();

        // SetUiExecutor posts tasks back to this window so UI work runs on UI thread.
        const HWND hwnd = GetSafeHwnd();
        bus_.SetUiExecutor([hwnd](const std::function<void()>& task) {
            if (hwnd == NULL || !::IsWindow(hwnd)) {
                return;
            }

            std::function<void()>* heapTask = new std::function<void()>(task);
            ::PostMessage(hwnd, WM_EB_UI_TASK, 0, reinterpret_cast<LPARAM>(heapTask));
        });

        loginToken_ = bus_.Subscribe<LoginEvent>(
            [this](const LoginEvent& e) {
                loginEventCount_.fetch_add(1);
                {
                    std::lock_guard<std::mutex> lock(loginStateMutex_);
                    lastLoginUser_ = e.user;
                }
                UpdateCaption();
            },
            eb::RegistrationRule::OneToOne);

        const std::uint64_t loginSecond = bus_.Subscribe<LoginEvent>(
            [](const LoginEvent& e) {
                UNREFERENCED_PARAMETER(e);
            },
            eb::RegistrationRule::OneToOne);

        loginRuleOk_ = (loginToken_ != 0 && loginSecond == 0);

        tickWorkerToken_ = bus_.Subscribe<TickEvent>(
            [this](const TickEvent& e) {
                UNREFERENCED_PARAMETER(e);
                workerTickCount_.fetch_add(1);
            },
            eb::RegistrationRule::OneToMany,
            eb::DispatchTarget::CurrentThread);

        tickUiToken_ = bus_.Subscribe<TickEvent>(
            [this](const TickEvent& e) {
                lastUiFrame_.store(e.frame);
                UpdateCaption();
            },
            eb::RegistrationRule::OneToMany,
            eb::DispatchTarget::UiThread);

        bus_.Publish(LoginEvent{"alice"});

        running_.store(true);
        producer_ = std::thread([this]() {
            int frame = 0;
            while (running_.load()) {
                bus_.PublishAsync(TickEvent{frame});
                ++frame;
                std::this_thread::sleep_for(std::chrono::milliseconds(300));
            }
        });

        SetTimer(kUiRefreshTimerId, 200, NULL);

        UpdateCaption();
        UpdateStatusText();
        return 0;
    }

    afx_msg void OnTimer(UINT_PTR nIDEvent) {
        if (nIDEvent == kUiRefreshTimerId) {
            UpdateStatusText();
        }
        CFrameWnd::OnTimer(nIDEvent);
    }

    afx_msg void OnPublishLoginClicked() {
        bus_.Publish(LoginEvent{"button_click"});
        UpdateStatusText();
    }

    afx_msg LRESULT OnEventBusUiTask(WPARAM wParam, LPARAM lParam) {
        UNREFERENCED_PARAMETER(wParam);

        std::unique_ptr<std::function<void()> > task(
            reinterpret_cast<std::function<void()>*>(lParam));

        if (task && *task) {
            (*task)();
        }
        return 0;
    }

    afx_msg void OnDestroy() {
        KillTimer(kUiRefreshTimerId);

        running_.store(false);
        if (producer_.joinable()) {
            producer_.join();
        }

        if (loginToken_ != 0) {
            bus_.Unsubscribe(loginToken_);
        }
        if (tickWorkerToken_ != 0) {
            bus_.Unsubscribe(tickWorkerToken_);
        }
        if (tickUiToken_ != 0) {
            bus_.Unsubscribe(tickUiToken_);
        }

        bus_.SetUiExecutor(std::function<void(std::function<void()>)>());
        bus_.StopAsyncWorker();

        CFrameWnd::OnDestroy();
    }

private:
    void UpdateStatusText() {
        CString loginUser;
        {
            std::lock_guard<std::mutex> lock(loginStateMutex_);
            loginUser = CString(lastLoginUser_.c_str());
        }

        CString status;
        status.Format(
            _T("Login one-to-one: %s\r\nLogin events handled: %d\r\nLast login user: %s\r\nWorker tick callbacks: %d\r\nLast UI frame: %d\r\nClick button to publish LoginEvent immediately."),
            loginRuleOk_ ? _T("OK") : _T("FAIL"),
            loginEventCount_.load(),
            loginUser.GetString(),
            workerTickCount_.load(),
            lastUiFrame_.load());
        statusText_.SetWindowText(status);
    }

    void UpdateCaption() {
        CString title;
        title.Format(
            _T("EventBus MFC Demo | LoginRule=%s | Logins=%d | WorkerTicks=%d | LastUiFrame=%d"),
            loginRuleOk_ ? _T("OK") : _T("FAIL"),
            loginEventCount_.load(),
            workerTickCount_.load(),
            lastUiFrame_.load());
        SetWindowText(title);
    }

private:
    eb::EventBus bus_;
    std::uint64_t loginToken_;
    std::uint64_t tickWorkerToken_;
    std::uint64_t tickUiToken_;
    std::atomic<bool> running_;
    std::thread producer_;
    std::atomic<int> loginEventCount_;
    std::atomic<int> workerTickCount_;
    std::atomic<int> lastUiFrame_;
    std::mutex loginStateMutex_;
    std::string lastLoginUser_;
    bool loginRuleOk_;
    CStatic statusText_;
    CButton publishLoginButton_;

    DECLARE_MESSAGE_MAP()
};

BEGIN_MESSAGE_MAP(CMainFrame, CFrameWnd)
    ON_WM_CREATE()
    ON_WM_TIMER()
    ON_BN_CLICKED(kControlPublishLogin, &CMainFrame::OnPublishLoginClicked)
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
