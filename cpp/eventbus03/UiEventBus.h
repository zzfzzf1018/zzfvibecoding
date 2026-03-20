#pragma once

#include <windows.h>

#include <functional>
#include <mutex>
#include <queue>
#include <string>
#include <unordered_map>

#include "EventBus.h"

namespace eb {

struct UiScenarioNoteEvent {
    std::string text;
};

struct UiRefreshViewEvent {
};

class UiEventBus : public EventBus {
public:
    struct UiTaskRuntimeStats {
        std::size_t pendingTaskCount;
        std::size_t droppedTaskCount;
    };

    UiEventBus()
        : maxPendingUiTasks_(4096),
          droppedUiTaskCount_(0),
                    uiRefreshPending_(false),
          dispatcherHwnd_(NULL),
          dispatcherThreadId_(::GetCurrentThreadId()) {
        if (EnsureUiDispatcher()) {
            SetUiExecutor([this](const std::function<void()>& task) {
                PostUiTask(task);
            });
        }
    }

    ~UiEventBus() {
        DisableUiExecutor();
        DestroyUiDispatcher();
        DisposePendingUiTasks();
    }

    bool EnableUiExecutor() {
        if (!EnsureUiDispatcher()) {
            return false;
        }

        SetUiExecutor([this](const std::function<void()>& task) {
            PostUiTask(task);
        });
        return true;
    }

    void DisableUiExecutor() {
        SetUiExecutor(std::function<void(std::function<void()>)>());
    }

    void BindUiDispatcher(HWND hwnd, UINT uiMessageId) {
        UNREFERENCED_PARAMETER(hwnd);
        UNREFERENCED_PARAMETER(uiMessageId);
        EnableUiExecutor();
    }

    void UnbindUiDispatcher() {
        DisableUiExecutor();
    }

    bool HandleUiTaskMessage(LPARAM lParam) {
        UNREFERENCED_PARAMETER(lParam);
        return false;
    }

    void DisposePendingUiTasks() {
        std::lock_guard<std::mutex> lock(uiMutex_);
        droppedUiTaskCount_ += pendingUiTasks_.size();
        std::queue<std::function<void()> > empty;
        pendingUiTasks_.swap(empty);
    }

    PublishStatus PublishUiScenarioNote(const std::string& text) {
        return PublishSync(UiScenarioNoteEvent{text});
    }

    PublishStatus PublishUiRefresh() {
        {
            std::lock_guard<std::mutex> lock(uiMutex_);
            if (uiRefreshPending_) {
                return PublishStatus::Ok;
            }
            uiRefreshPending_ = true;
        }

        const PublishStatus st = PublishSync(UiRefreshViewEvent{});
        if (st != PublishStatus::Ok) {
            std::lock_guard<std::mutex> lock(uiMutex_);
            uiRefreshPending_ = false;
        }
        return st;
    }

    void ConfigurePendingUiTaskLimit(std::size_t maxTasks) {
        std::lock_guard<std::mutex> lock(uiMutex_);
        maxPendingUiTasks_ = maxTasks == 0 ? 1 : maxTasks;
    }

    std::size_t DroppedUiTaskCount() const {
        std::lock_guard<std::mutex> lock(uiMutex_);
        return droppedUiTaskCount_;
    }

    std::size_t PendingUiTaskCount() const {
        std::lock_guard<std::mutex> lock(uiMutex_);
        return pendingUiTasks_.size();
    }

    UiTaskRuntimeStats GetUiTaskRuntimeStats() const {
        std::lock_guard<std::mutex> lock(uiMutex_);
        UiTaskRuntimeStats stats;
        stats.pendingTaskCount = pendingUiTasks_.size();
        stats.droppedTaskCount = droppedUiTaskCount_;
        return stats;
    }

    void ResetUiTaskStats() {
        std::lock_guard<std::mutex> lock(uiMutex_);
        droppedUiTaskCount_ = 0;
    }

    template <typename Obj>
    std::uint64_t SubscribeUiScenarioNote(
        Obj* obj,
        void (Obj::*method)(const UiScenarioNoteEvent&),
        RegistrationRule rule = RegistrationRule::OneToMany) {
        return Subscribe<UiScenarioNoteEvent>(obj, method, rule, DispatchTarget::UiThread);
    }

    template <typename Obj>
    std::uint64_t SubscribeUiRefresh(
        Obj* obj,
        void (Obj::*method)(const UiRefreshViewEvent&),
        RegistrationRule rule = RegistrationRule::OneToMany) {
        return Subscribe<UiRefreshViewEvent>(
            [this, obj, method](const UiRefreshViewEvent& e) {
                (obj->*method)(e);
                std::lock_guard<std::mutex> lock(uiMutex_);
                uiRefreshPending_ = false;
            },
            rule,
            DispatchTarget::UiThread);
    }

private:
    static LRESULT CALLBACK UiDispatcherWndProc(HWND hwnd, UINT message, WPARAM wParam, LPARAM lParam) {
        if (message == WM_NCCREATE) {
            CREATESTRUCT* cs = reinterpret_cast<CREATESTRUCT*>(lParam);
            if (cs != NULL) {
                UiEventBus* self = reinterpret_cast<UiEventBus*>(cs->lpCreateParams);
                if (self != NULL) {
                    ::SetWindowLongPtr(hwnd, GWLP_USERDATA, reinterpret_cast<LONG_PTR>(self));
                }
            }
            return ::DefWindowProc(hwnd, message, wParam, lParam);
        }

        UiEventBus* self = reinterpret_cast<UiEventBus*>(::GetWindowLongPtr(hwnd, GWLP_USERDATA));
        if (self == NULL) {
            return ::DefWindowProc(hwnd, message, wParam, lParam);
        }

        if (message == kUiTaskMessage) {
            self->DrainUiTasks();
            return 0;
        }

        if (message == WM_NCDESTROY) {
            ::SetWindowLongPtr(hwnd, GWLP_USERDATA, 0);
        }

        return ::DefWindowProc(hwnd, message, wParam, lParam);
    }

    bool EnsureUiDispatcher() {
        if (dispatcherHwnd_ != NULL && ::IsWindow(dispatcherHwnd_)) {
            return true;
        }

        static const wchar_t* kClassName = L"EbUiEventBusDispatcherWindow";
        static bool classRegistered = false;
        static std::mutex classMutex;

        {
            std::lock_guard<std::mutex> lock(classMutex);
            if (!classRegistered) {
                WNDCLASS wc;
                ::ZeroMemory(&wc, sizeof(wc));
                wc.lpfnWndProc = &UiDispatcherWndProc;
                wc.hInstance = ::GetModuleHandle(NULL);
                wc.lpszClassName = kClassName;
                const ATOM atom = ::RegisterClass(&wc);
                if (atom == 0 && ::GetLastError() != ERROR_CLASS_ALREADY_EXISTS) {
                    return false;
                }
                classRegistered = true;
            }
        }

        dispatcherThreadId_ = ::GetCurrentThreadId();
        dispatcherHwnd_ = ::CreateWindowEx(
            0,
            kClassName,
            L"",
            0,
            0,
            0,
            0,
            0,
            HWND_MESSAGE,
            NULL,
            ::GetModuleHandle(NULL),
            this);
        return dispatcherHwnd_ != NULL;
    }

    void DestroyUiDispatcher() {
        HWND hwnd = NULL;
        {
            std::lock_guard<std::mutex> lock(uiMutex_);
            hwnd = dispatcherHwnd_;
            dispatcherHwnd_ = NULL;
        }

        if (hwnd != NULL && ::IsWindow(hwnd)) {
            ::DestroyWindow(hwnd);
        }
    }

    void DrainUiTasks() {
        for (;;) {
            std::function<void()> task;
            {
                std::lock_guard<std::mutex> lock(uiMutex_);
                if (pendingUiTasks_.empty()) {
                    break;
                }
                task = pendingUiTasks_.front();
                pendingUiTasks_.pop();
            }

            try {
                task();
            } catch (...) {
                // Keep draining queued UI tasks even if one callback throws.
            }
        }
    }

    void PostUiTask(const std::function<void()>& task) {
        if (::GetCurrentThreadId() == dispatcherThreadId_) {
            task();
            return;
        }

        HWND hwnd = NULL;
        {
            std::lock_guard<std::mutex> lock(uiMutex_);
            hwnd = dispatcherHwnd_;
            if (pendingUiTasks_.size() >= maxPendingUiTasks_) {
                ++droppedUiTaskCount_;
                return;
            }
            pendingUiTasks_.push(task);
        }

        if (hwnd == NULL || !::IsWindow(hwnd)) {
            std::lock_guard<std::mutex> lock(uiMutex_);
            if (!pendingUiTasks_.empty()) {
                pendingUiTasks_.pop();
                ++droppedUiTaskCount_;
            }
            return;
        }

        if (!::PostMessage(hwnd, kUiTaskMessage, 0, 0)) {
            std::lock_guard<std::mutex> lock(uiMutex_);
            if (!pendingUiTasks_.empty()) {
                pendingUiTasks_.pop();
                ++droppedUiTaskCount_;
            }
        }
    }

private:
    static const UINT kUiTaskMessage = WM_APP + 0x4B1;
    mutable std::mutex uiMutex_;
    std::queue<std::function<void()> > pendingUiTasks_;
    std::size_t maxPendingUiTasks_;
    std::size_t droppedUiTaskCount_;
    bool uiRefreshPending_;
    HWND dispatcherHwnd_;
    DWORD dispatcherThreadId_;
};

}  // namespace eb
