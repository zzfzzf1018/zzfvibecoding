#pragma once

#include <windows.h>

#include <functional>
#include <mutex>
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
        : hwnd_(NULL), uiMessageId_(0), nextTaskId_(1), maxPendingUiTasks_(4096), droppedUiTaskCount_(0) {}

    ~UiEventBus() {
        UnbindUiDispatcher();
        DisposePendingUiTasks();
    }

    void BindUiDispatcher(HWND hwnd, UINT uiMessageId) {
        {
            std::lock_guard<std::mutex> lock(uiMutex_);
            hwnd_ = hwnd;
            uiMessageId_ = uiMessageId;
        }

        SetUiExecutor([this](const std::function<void()>& task) {
            PostUiTask(task);
        });
    }

    void UnbindUiDispatcher() {
        SetUiExecutor(std::function<void(std::function<void()>)>());
        std::lock_guard<std::mutex> lock(uiMutex_);
        hwnd_ = NULL;
        uiMessageId_ = 0;
        droppedUiTaskCount_ += pendingUiTasks_.size();
        pendingUiTasks_.clear();
    }

    bool HandleUiTaskMessage(LPARAM lParam) {
        if (lParam == 0) {
            return false;
        }

        const ULONG_PTR taskId = static_cast<ULONG_PTR>(lParam);
        std::function<void()> task;
        {
            std::lock_guard<std::mutex> lock(uiMutex_);
            std::unordered_map<ULONG_PTR, std::function<void()> >::iterator it = pendingUiTasks_.find(taskId);
            if (it == pendingUiTasks_.end()) {
                return false;
            }
            task = it->second;
            pendingUiTasks_.erase(it);
        }

        try {
            task();
        } catch (...) {
            return false;
        }

        return true;
    }

    void DisposePendingUiTasks() {
        std::lock_guard<std::mutex> lock(uiMutex_);
        droppedUiTaskCount_ += pendingUiTasks_.size();
        pendingUiTasks_.clear();
    }

    PublishStatus PublishUiScenarioNote(const std::string& text) {
        return PublishSync(UiScenarioNoteEvent{text});
    }

    PublishStatus PublishUiRefresh() {
        return PublishSync(UiRefreshViewEvent{});
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
        return Subscribe<UiRefreshViewEvent>(obj, method, rule, DispatchTarget::UiThread);
    }

private:
    void PostUiTask(const std::function<void()>& task) {
        HWND hwnd = NULL;
        UINT msg = 0;
        ULONG_PTR taskId = 0;
        {
            std::lock_guard<std::mutex> lock(uiMutex_);
            hwnd = hwnd_;
            msg = uiMessageId_;
            if (pendingUiTasks_.size() >= maxPendingUiTasks_) {
                ++droppedUiTaskCount_;
                return;
            }
            do {
                taskId = nextTaskId_++;
                if (taskId == 0) {
                    taskId = nextTaskId_++;
                }
            } while (pendingUiTasks_.find(taskId) != pendingUiTasks_.end());
            pendingUiTasks_[taskId] = task;
        }

        if (hwnd == NULL || msg == 0 || !::IsWindow(hwnd)) {
            std::lock_guard<std::mutex> lock(uiMutex_);
            if (pendingUiTasks_.erase(taskId) > 0) {
                ++droppedUiTaskCount_;
            }
            return;
        }

        if (!::PostMessage(hwnd, msg, 0, static_cast<LPARAM>(taskId))) {
            std::lock_guard<std::mutex> lock(uiMutex_);
            if (pendingUiTasks_.erase(taskId) > 0) {
                ++droppedUiTaskCount_;
            }
        }
    }

private:
    mutable std::mutex uiMutex_;
    std::unordered_map<ULONG_PTR, std::function<void()> > pendingUiTasks_;
    ULONG_PTR nextTaskId_;
    std::size_t maxPendingUiTasks_;
    std::size_t droppedUiTaskCount_;
    HWND hwnd_;
    UINT uiMessageId_;
};

}  // namespace eb
