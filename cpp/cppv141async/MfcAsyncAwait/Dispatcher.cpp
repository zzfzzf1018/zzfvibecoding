#include "Dispatcher.h"

namespace
{
constexpr wchar_t kDispatcherWindowClassName[] = L"CppV141AsyncDispatcherWindow";
constexpr UINT kResumeCoroutineMessage = WM_APP + 0x41;

DWORD WINAPI ResumeOnThreadPool(LPVOID context)
{
    auto handle = std::experimental::coroutine_handle<>::from_address(context);
    handle.resume();
    return 0;
}
}

namespace cppv141async
{
ui_dispatcher::ui_dispatcher() noexcept
    : hwnd_(nullptr)
    , thread_id_(0)
    , class_atom_(0)
{
}

ui_dispatcher& ui_dispatcher::instance() noexcept
{
    static ui_dispatcher dispatcher;
    return dispatcher;
}

bool ui_dispatcher::initialize(HINSTANCE instance_handle)
{
    if (hwnd_ != nullptr)
    {
        return true;
    }

    thread_id_ = ::GetCurrentThreadId();

    WNDCLASSW window_class = {};
    window_class.lpfnWndProc = &ui_dispatcher::window_proc;
    window_class.hInstance = instance_handle;
    window_class.lpszClassName = kDispatcherWindowClassName;

    class_atom_ = ::RegisterClassW(&window_class);
    if (class_atom_ == 0)
    {
        const DWORD error = ::GetLastError();
        if (error != ERROR_CLASS_ALREADY_EXISTS)
        {
            return false;
        }
    }

    hwnd_ = ::CreateWindowExW(
        0,
        kDispatcherWindowClassName,
        L"",
        0,
        0,
        0,
        0,
        0,
        HWND_MESSAGE,
        nullptr,
        instance_handle,
        nullptr);

    return hwnd_ != nullptr;
}

void ui_dispatcher::shutdown() noexcept
{
    if (hwnd_ != nullptr)
    {
        ::DestroyWindow(hwnd_);
        hwnd_ = nullptr;
    }
}

bool ui_dispatcher::post(std::experimental::coroutine_handle<> handle) const noexcept
{
    if (hwnd_ == nullptr)
    {
        return false;
    }

    return ::PostMessageW(hwnd_, kResumeCoroutineMessage, 0, reinterpret_cast<LPARAM>(handle.address())) != FALSE;
}

DWORD ui_dispatcher::thread_id() const noexcept
{
    return thread_id_;
}

LRESULT CALLBACK ui_dispatcher::window_proc(HWND hwnd, UINT message, WPARAM wparam, LPARAM lparam) noexcept
{
    if (message == kResumeCoroutineMessage)
    {
        auto handle = std::experimental::coroutine_handle<>::from_address(reinterpret_cast<void*>(lparam));
        handle.resume();
        return 0;
    }

    return ::DefWindowProcW(hwnd, message, wparam, lparam);
}

void background_awaiter::await_suspend(std::experimental::coroutine_handle<> handle) const noexcept
{
    if (!::QueueUserWorkItem(&ResumeOnThreadPool, handle.address(), WT_EXECUTELONGFUNCTION))
    {
        handle.resume();
    }
}
}
