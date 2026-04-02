#include "pch.h"

#include "MainFrame.h"
#include "UiEventBusApp.h"
#include "resource.h"

namespace {
constexpr int kMargin = 12;
constexpr int kButtonHeight = 32;
constexpr int kStatusHeight = 24;
}  // namespace

BEGIN_MESSAGE_MAP(CMainFrame, CFrameWnd)
    ON_WM_CREATE()
    ON_WM_SIZE()
    ON_COMMAND(ID_START_BACKGROUND_TASK, &CMainFrame::onStartBackgroundTask)
    ON_WM_CLOSE()
END_MESSAGE_MAP()

CMainFrame::CMainFrame()
        : m_eventBus(GetApp().getEventBus()),
            m_callbackLifetime(std::make_shared<CallbackLifetime>()) {
    Create(nullptr, _T("MFC UI EventBus Demo"), WS_OVERLAPPEDWINDOW, CRect(100, 100, 900, 600));

    std::weak_ptr<CallbackLifetime> weakLifetime = m_callbackLifetime;
    m_businessFinishedSubscription = m_eventBus.subscribe<BusinessTaskFinishedEvent>(
        [this, weakLifetime](const BusinessTaskFinishedEvent& event) {
            if (weakLifetime.expired()) {
                return;
            }

            onBusinessTaskFinished(event);
        },
        DispatchPolicy::UiThread);
}

CMainFrame::~CMainFrame() {
    m_businessFinishedSubscription.reset();
    m_callbackLifetime.reset();
    joinWorkers();
}

int CMainFrame::OnCreate(LPCREATESTRUCT createStruct) {
    if (CFrameWnd::OnCreate(createStruct) == -1) {
        return -1;
    }

    const DWORD uiThreadId = GetApp().getUiDispatcher().getUiThreadId();

    m_startButton.Create(
        _T("Start Background Task"),
        WS_CHILD | WS_VISIBLE | WS_TABSTOP | BS_PUSHBUTTON,
        CRect(0, 0, 0, 0),
        this,
        ID_START_BACKGROUND_TASK);

    CString initial_status;
    initial_status.Format(_T("UI thread is ready. ThreadId=%lu"), uiThreadId);

    m_statusLabel.Create(
        initial_status,
        WS_CHILD | WS_VISIBLE,
        CRect(0, 0, 0, 0),
        this,
        IDC_STATUS_STATIC);

    m_logList.Create(
        WS_CHILD | WS_VISIBLE | WS_VSCROLL | LBS_NOINTEGRALHEIGHT | WS_BORDER,
        CRect(0, 0, 0, 0),
        this,
        IDC_LOG_LIST);

    appendLog(_T("Main window created."));
    appendLog(_T("Click the button to start a worker thread."));
    return 0;
}

void CMainFrame::OnSize(UINT type, int cx, int cy) {
    CFrameWnd::OnSize(type, cx, cy);
    layoutControls(cx, cy);
}

void CMainFrame::onStartBackgroundTask() {
    startBusinessTask();
}

void CMainFrame::OnClose() {
    m_shuttingDown.store(true);
    joinWorkers();
    CFrameWnd::OnClose();
}

void CMainFrame::layoutControls(int clientWidth, int clientHeight) {
    if (!::IsWindow(m_startButton.GetSafeHwnd())) {
        return;
    }

    const int contentWidth = std::max(0, clientWidth - (2 * kMargin));
    m_startButton.MoveWindow(kMargin, kMargin, 220, kButtonHeight);
    m_statusLabel.MoveWindow(kMargin, kMargin + kButtonHeight + 10, contentWidth, kStatusHeight);

    const int logTop = kMargin + kButtonHeight + 10 + kStatusHeight + 10;
    const int logHeight = std::max(0, clientHeight - logTop - kMargin);
    m_logList.MoveWindow(kMargin, logTop, contentWidth, logHeight);
}

void CMainFrame::appendLog(const CString& message) {
    if (!::IsWindow(m_logList.GetSafeHwnd())) {
        return;
    }

    m_logList.AddString(message);
    const int count = m_logList.GetCount();
    if (count > 0) {
        m_logList.SetCurSel(count - 1);
    }
}

void CMainFrame::startBusinessTask() {
    const int taskId = m_nextTaskId.fetch_add(1);
    CString statusMessage;
    statusMessage.Format(_T("Task %d is running. UI ThreadId=%lu"), taskId, ::GetCurrentThreadId());
    m_statusLabel.SetWindowText(statusMessage);

    CString logMessage;
    logMessage.Format(_T("Queued task %d on worker thread."), taskId);
    appendLog(logMessage);

    {
        std::lock_guard<std::mutex> lock(m_workersMutex);
        m_workerThreads.emplace_back([this, taskId]() {
            ::Sleep(1200);
            if (m_shuttingDown.load()) {
                return;
            }

            BusinessTaskFinishedEvent event;
            event.task_id = taskId;
            event.worker_thread_id = ::GetCurrentThreadId();
            event.ui_thread_id = GetApp().getUiDispatcher().getUiThreadId();
            event.result_text.Format(_T("Task %d finished on worker thread %lu"), taskId, event.worker_thread_id);

            m_eventBus.publish(event);
        });
    }
}

void CMainFrame::onBusinessTaskFinished(const BusinessTaskFinishedEvent& event) {
    CString statusMessage;
    statusMessage.Format(
        _T("Task %d completed. Worker=%lu UI=%lu Current=%lu"),
        event.task_id,
        event.worker_thread_id,
        event.ui_thread_id,
        ::GetCurrentThreadId());
    m_statusLabel.SetWindowText(statusMessage);

    CString logMessage;
    logMessage.Format(
        _T("Received task %d on UI thread. worker=%lu ui=%lu current=%lu"),
        event.task_id,
        event.worker_thread_id,
        event.ui_thread_id,
        ::GetCurrentThreadId());
    appendLog(logMessage);
    appendLog(event.result_text);
}

void CMainFrame::joinWorkers() {
    std::vector<std::thread> threads;
    {
        std::lock_guard<std::mutex> lock(m_workersMutex);
        threads.swap(m_workerThreads);
    }

    for (auto& thread : threads) {
        if (thread.joinable()) {
            thread.join();
        }
    }
}