#pragma once

#include <eventbus/EventBus.h>
#include "Events.h"

class CMainFrame : public CFrameWnd {
public:
    CMainFrame();
    ~CMainFrame() override;

protected:
    afx_msg int OnCreate(LPCREATESTRUCT createStruct);
    afx_msg void OnSize(UINT type, int cx, int cy);
    afx_msg void onStartBackgroundTask();
    afx_msg void OnClose();
    DECLARE_MESSAGE_MAP()

private:
    struct CallbackLifetime {};

    void layoutControls(int clientWidth, int clientHeight);
    void appendLog(const CString& message);
    void startBusinessTask();
    void onBusinessTaskFinished(const BusinessTaskFinishedEvent& event);
    void joinWorkers();

    CButton m_startButton;
    CStatic m_statusLabel;
    CListBox m_logList;

    EventBus& m_eventBus;
    SubscriptionToken m_businessFinishedSubscription;
    std::shared_ptr<CallbackLifetime> m_callbackLifetime;
    std::atomic<int> m_nextTaskId{1};
    std::atomic<bool> m_shuttingDown{false};
    std::mutex m_workersMutex;
    std::vector<std::thread> m_workerThreads;
};