#pragma once

#include <Windows.h>

#include <atomic>
#include <functional>
#include <mutex>
#include <vector>

// Owns the UI-thread task queue and the wakeup mechanism that causes queued
// work to be drained back on the registered UI thread.
class UiDispatcher {
public:
    using Task = std::function<void()>;

    UiDispatcher();
    ~UiDispatcher();

    UiDispatcher(const UiDispatcher&) = delete;
    UiDispatcher& operator=(const UiDispatcher&) = delete;

    // Initializes the dispatcher on the current UI thread.
    // Default MFC hosts typically call this once during app startup.
    bool initializeForCurrentThread();

    // Stops dispatching, clears pending tasks, and releases wake resources.
    void shutdown();

    // Advanced mode: explicitly route wakeup messages to a caller-owned window.
    // Most MFC hosts do not need this because initializeForCurrentThread()
    // creates an internal hidden wake window automatically.
    // Call this only after initializeForCurrentThread() succeeds, and only for
    // a window whose lifetime is managed on the same UI thread.
    void setMessageTarget(HWND wakeWindow, UINT wakeMessage);

    // Clears an explicit wake window binding and falls back to the internal
    // hidden wake window when available.
    void clearMessageTarget();

    // Can be called from any thread. Queues a task for UI-thread execution.
    bool post(Task task);

    // Runs all queued UI tasks. Normally triggered by the dispatcher's wakeup
    // mechanism rather than called directly by application code.
    void drain();

    // Exposes the event-handle wake source for custom loops that use
    // UiPumpHelper or their own waiting strategy.
    HANDLE getWakeHandle() const;

    // Returns true only when called from the thread that initialized the
    // dispatcher.
    bool isUiThread() const;

    // Returns the registered UI thread id after initialization.
    DWORD getUiThreadId() const;

private:
    bool createInternalWakeWindow();
    void destroyInternalWakeWindow();

    static LRESULT CALLBACK internalWakeWindowProc(HWND hwnd, UINT message, WPARAM wParam, LPARAM lParam);

    DWORD m_uiThreadId;
    HANDLE m_eventHandle;
    HWND m_internalWakeWindow;
    HWND m_wakeWindow;
    UINT m_wakeMessage;
    bool m_messagePending;
    bool m_stopped;
    std::mutex m_queueMutex;
    std::vector<Task> m_pendingTasks;
};