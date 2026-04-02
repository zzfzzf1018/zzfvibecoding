#include "eventbus/UiDispatcher.h"

#include <utility>

namespace {
constexpr wchar_t kUiDispatcherWindowClassName[] = L"EventBusUiDispatcherWindow";
constexpr UINT kInternalWakeMessage = WM_APP + 1;
}  // namespace

UiDispatcher::UiDispatcher()
        : m_uiThreadId(0),
            m_eventHandle(nullptr),
            m_internalWakeWindow(nullptr),
            m_wakeWindow(nullptr),
            m_wakeMessage(0),
            m_messagePending(false),
            m_stopped(false) {
}

UiDispatcher::~UiDispatcher() {
    shutdown();
}

bool UiDispatcher::initializeForCurrentThread() {
    if (m_eventHandle != nullptr && m_internalWakeWindow != nullptr) {
        return true;
    }

    if (m_uiThreadId == 0) {
        m_uiThreadId = ::GetCurrentThreadId();
    }

    if (m_eventHandle == nullptr) {
        m_eventHandle = ::CreateEvent(nullptr, FALSE, FALSE, nullptr);
    }

    if (m_internalWakeWindow == nullptr && !createInternalWakeWindow()) {
        return false;
    }

    m_stopped = false;
    return m_eventHandle != nullptr && m_internalWakeWindow != nullptr;
}

bool UiDispatcher::createInternalWakeWindow() {
    static ATOM atom = 0;

    if (m_internalWakeWindow != nullptr) {
        return true;
    }

    if (atom == 0) {
        WNDCLASSEX window_class = {};
        window_class.cbSize = sizeof(window_class);
        window_class.lpfnWndProc = &UiDispatcher::internalWakeWindowProc;
        window_class.hInstance = ::GetModuleHandle(nullptr);
        window_class.lpszClassName = kUiDispatcherWindowClassName;

        atom = ::RegisterClassEx(&window_class);
        if (atom == 0 && ::GetLastError() != ERROR_CLASS_ALREADY_EXISTS) {
            return false;
        }
    }

    m_internalWakeWindow = ::CreateWindowEx(
        0,
        kUiDispatcherWindowClassName,
        L"",
        0,
        0,
        0,
        0,
        0,
        HWND_MESSAGE,
        nullptr,
        ::GetModuleHandle(nullptr),
        this);
    return m_internalWakeWindow != nullptr;
}

void UiDispatcher::destroyInternalWakeWindow() {
    if (m_internalWakeWindow == nullptr) {
        return;
    }

    if (isUiThread() && ::IsWindow(m_internalWakeWindow)) {
        ::DestroyWindow(m_internalWakeWindow);
    }

    m_internalWakeWindow = nullptr;
}

void UiDispatcher::setMessageTarget(HWND wakeWindow, UINT wakeMessage) {
    std::lock_guard<std::mutex> lock(m_queueMutex);
    m_wakeWindow = wakeWindow;
    m_wakeMessage = wakeMessage;
    m_messagePending = false;
}

void UiDispatcher::clearMessageTarget() {
    std::lock_guard<std::mutex> lock(m_queueMutex);
    m_wakeWindow = nullptr;
    m_wakeMessage = 0;
    m_messagePending = false;
}

void UiDispatcher::shutdown() {
    HANDLE handleToClose = nullptr;
    bool shouldDestroyInternalWakeWindow = false;
    {
        std::lock_guard<std::mutex> lock(m_queueMutex);
        if (m_eventHandle == nullptr && m_internalWakeWindow == nullptr && m_stopped) {
            return;
        }

        m_stopped = true;
        m_pendingTasks.clear();
        m_messagePending = false;
        m_wakeWindow = nullptr;
        m_wakeMessage = 0;
        shouldDestroyInternalWakeWindow = m_internalWakeWindow != nullptr;
        handleToClose = m_eventHandle;
        m_eventHandle = nullptr;
    }

    if (shouldDestroyInternalWakeWindow) {
        destroyInternalWakeWindow();
    }

    if (handleToClose != nullptr) {
        ::CloseHandle(handleToClose);
    }
}

bool UiDispatcher::post(Task task) {
    HANDLE wakeHandle = nullptr;
    HWND wakeWindow = nullptr;
    UINT wakeMessage = 0;
    bool shouldPostMessage = false;

    {
        std::lock_guard<std::mutex> lock(m_queueMutex);
        if (m_stopped) {
            return false;
        }

        m_pendingTasks.push_back(std::move(task));

        if (m_wakeWindow != nullptr && m_wakeMessage != 0) {
            wakeWindow = m_wakeWindow;
            wakeMessage = m_wakeMessage;
        } else if (m_internalWakeWindow != nullptr) {
            wakeWindow = m_internalWakeWindow;
            wakeMessage = kInternalWakeMessage;
        }

        if (wakeWindow != nullptr && wakeMessage != 0) {
            if (!m_messagePending) {
                m_messagePending = true;
                shouldPostMessage = true;
            }
        } else {
            wakeHandle = m_eventHandle;
        }
    }

    if (shouldPostMessage) {
        if (::PostMessage(wakeWindow, wakeMessage, 0, 0) != FALSE) {
            return true;
        }

        std::lock_guard<std::mutex> lock(m_queueMutex);
        m_messagePending = false;
        return false;
    }

    if (wakeHandle == nullptr) {
        return true;
    }

    return ::SetEvent(wakeHandle) != FALSE;
}

void UiDispatcher::drain() {
    std::vector<Task> tasks;
    {
        std::lock_guard<std::mutex> lock(m_queueMutex);
        m_messagePending = false;
        tasks.swap(m_pendingTasks);
    }

    for (const auto& task : tasks) {
        if (task) {
            task();
        }
    }
}

HANDLE UiDispatcher::getWakeHandle() const {
    return m_eventHandle;
}

bool UiDispatcher::isUiThread() const {
    return m_uiThreadId != 0 && ::GetCurrentThreadId() == m_uiThreadId;
}

DWORD UiDispatcher::getUiThreadId() const {
    return m_uiThreadId;
}

LRESULT CALLBACK UiDispatcher::internalWakeWindowProc(HWND hwnd, UINT message, WPARAM wParam, LPARAM lParam) {
    UiDispatcher* dispatcher = reinterpret_cast<UiDispatcher*>(::GetWindowLongPtr(hwnd, GWLP_USERDATA));

    if (message == WM_NCCREATE) {
        auto* createStruct = reinterpret_cast<CREATESTRUCT*>(lParam);
        dispatcher = static_cast<UiDispatcher*>(createStruct->lpCreateParams);
        if (dispatcher != nullptr) {
            ::SetWindowLongPtr(hwnd, GWLP_USERDATA, reinterpret_cast<LONG_PTR>(dispatcher));
        }
        return TRUE;
    }

    if (message == WM_NCDESTROY) {
        ::SetWindowLongPtr(hwnd, GWLP_USERDATA, 0);
        return ::DefWindowProc(hwnd, message, wParam, lParam);
    }

    if (message == kInternalWakeMessage && dispatcher != nullptr) {
        dispatcher->drain();
        return 0;
    }

    return ::DefWindowProc(hwnd, message, wParam, lParam);
}