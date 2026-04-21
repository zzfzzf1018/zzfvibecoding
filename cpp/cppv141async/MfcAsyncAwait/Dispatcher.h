#pragma once

#include <afxwin.h>

#include <experimental/coroutine>

namespace cppv141async
{
class ui_dispatcher
{
public:
    static ui_dispatcher& instance() noexcept;

    bool initialize(HINSTANCE instance_handle = ::GetModuleHandleW(nullptr));
    void shutdown() noexcept;
    bool post(std::experimental::coroutine_handle<> handle) const noexcept;
    DWORD thread_id() const noexcept;

private:
    ui_dispatcher() noexcept;
    ~ui_dispatcher() = default;

    ui_dispatcher(const ui_dispatcher&) = delete;
    ui_dispatcher& operator=(const ui_dispatcher&) = delete;

    static LRESULT CALLBACK window_proc(HWND hwnd, UINT message, WPARAM wparam, LPARAM lparam) noexcept;

private:
    HWND hwnd_;
    DWORD thread_id_;
    ATOM class_atom_;
};

struct background_awaiter
{
    bool await_ready() const noexcept
    {
        return false;
    }

    void await_suspend(std::experimental::coroutine_handle<> handle) const noexcept;

    void await_resume() const noexcept
    {
    }
};

struct ui_awaiter
{
    bool await_ready() const noexcept
    {
        return ::GetCurrentThreadId() == ui_dispatcher::instance().thread_id();
    }

    void await_suspend(std::experimental::coroutine_handle<> handle) const noexcept
    {
        if (!ui_dispatcher::instance().post(handle))
        {
            handle.resume();
        }
    }

    void await_resume() const noexcept
    {
    }
};

inline background_awaiter resume_background() noexcept
{
    return {};
}

inline ui_awaiter resume_ui() noexcept
{
    return {};
}
}