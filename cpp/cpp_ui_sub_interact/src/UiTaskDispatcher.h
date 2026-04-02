#pragma once

#include <Windows.h>

#include <atomic>
#include <functional>
#include <mutex>
#include <queue>

class UiTaskDispatcher
{
public:
    typedef std::function<void()> Task;

    UiTaskDispatcher()
        : window_(NULL), messageId_(0), active_(false)
    {
    }

    void Initialize(HWND window, UINT messageId)
    {
        std::lock_guard<std::mutex> lock(mutex_);
        window_ = window;
        messageId_ = messageId;
        active_.store(true);
    }

    void Shutdown()
    {
        std::lock_guard<std::mutex> lock(mutex_);
        active_.store(false);
        window_ = NULL;

        std::queue<Task> empty;
        tasks_.swap(empty);
    }

    bool PostTask(Task task)
    {
        HWND window = NULL;
        UINT messageId = 0;

        {
            std::lock_guard<std::mutex> lock(mutex_);
            if (!active_.load() || window_ == NULL || messageId_ == 0)
            {
                return false;
            }

            tasks_.push(std::move(task));
            window = window_;
            messageId = messageId_;
        }

        return ::PostMessage(window, messageId, 0, 0) != FALSE;
    }

    void DispatchPendingTasks()
    {
        std::queue<Task> pendingTasks;

        {
            std::lock_guard<std::mutex> lock(mutex_);
            pendingTasks.swap(tasks_);
        }

        while (!pendingTasks.empty())
        {
            Task task = std::move(pendingTasks.front());
            pendingTasks.pop();

            if (task)
            {
                task();
            }
        }
    }

private:
    HWND window_;
    UINT messageId_;
    std::atomic<bool> active_;
    std::mutex mutex_;
    std::queue<Task> tasks_;
};