#pragma once

#include <afxwin.h>

#include <exception>
#include <functional>
#include <memory>
#include <experimental/coroutine>
#include <optional>
#include <tuple>
#include <type_traits>
#include <utility>

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

template <typename T>
class task;

namespace details
{
template <typename T>
using decay_t = typename std::decay<T>::type;

template <typename T>
using remove_cvref_t = typename std::remove_cv<typename std::remove_reference<T>::type>::type;

template <typename T>
struct final_awaiter
{
    bool await_ready() const noexcept
    {
        return false;
    }

    template <typename Promise>
    void await_suspend(std::experimental::coroutine_handle<Promise> handle) const noexcept
    {
        auto continuation = handle.promise().continuation_;
        if (continuation)
        {
            continuation.resume();
        }
    }

    void await_resume() const noexcept
    {
    }
};

template <typename Object, typename Method, typename Tuple, size_t... Indices>
auto invoke_member_from_tuple(Object& object, Method method, Tuple& arguments, std::index_sequence<Indices...>)
    -> typename std::invoke_result<Method, Object&, decay_t<decltype(std::get<Indices>(arguments))>...>::type
{
    return std::mem_fn(method)(object, std::get<Indices>(arguments)...);
}
}

template <typename T>
class task
{
public:
    struct promise_type
    {
        using handle_type = std::experimental::coroutine_handle<promise_type>;

        task get_return_object() noexcept
        {
            return task(handle_type::from_promise(*this));
        }

        std::experimental::suspend_always initial_suspend() const noexcept
        {
            return {};
        }

        details::final_awaiter<T> final_suspend() const noexcept
        {
            return {};
        }

        template <typename U>
        void return_value(U&& value)
        {
            value_ = std::forward<U>(value);
        }

        void unhandled_exception() noexcept
        {
            exception_ = std::current_exception();
        }

        std::experimental::coroutine_handle<> continuation_;
        std::optional<T> value_;
        std::exception_ptr exception_;
    };

    using handle_type = std::experimental::coroutine_handle<promise_type>;

    task() noexcept
        : handle_(nullptr)
    {
    }

    explicit task(handle_type handle) noexcept
        : handle_(handle)
    {
    }

    task(task&& other) noexcept
        : handle_(other.handle_)
    {
        other.handle_ = nullptr;
    }

    task& operator=(task&& other) noexcept
    {
        if (this != &other)
        {
            if (handle_)
            {
                handle_.destroy();
            }

            handle_ = other.handle_;
            other.handle_ = nullptr;
        }

        return *this;
    }

    task(const task&) = delete;
    task& operator=(const task&) = delete;

    ~task()
    {
        if (handle_)
        {
            handle_.destroy();
        }
    }

    class awaiter
    {
    public:
        explicit awaiter(handle_type handle) noexcept
            : handle_(handle)
        {
        }

        awaiter(const awaiter&) = delete;
        awaiter& operator=(const awaiter&) = delete;

        awaiter(awaiter&& other) noexcept
            : handle_(other.handle_)
        {
            other.handle_ = nullptr;
        }

        awaiter& operator=(awaiter&& other) noexcept
        {
            if (this != &other)
            {
                if (handle_)
                {
                    handle_.destroy();
                }

                handle_ = other.handle_;
                other.handle_ = nullptr;
            }

            return *this;
        }

        ~awaiter()
        {
            if (handle_)
            {
                handle_.destroy();
            }
        }

        bool await_ready() const noexcept
        {
            return !handle_ || handle_.done();
        }

        void await_suspend(std::experimental::coroutine_handle<> continuation) noexcept
        {
            handle_.promise().continuation_ = continuation;
            handle_.resume();
        }

        T await_resume()
        {
            auto& promise = handle_.promise();
            if (promise.exception_)
            {
                std::rethrow_exception(promise.exception_);
            }

            return std::move(*promise.value_);
        }

    private:
        handle_type handle_;
    };

    awaiter operator co_await() && noexcept
    {
        auto handle = handle_;
        handle_ = nullptr;
        return awaiter(handle);
    }

private:
    handle_type handle_;
};

template <>
class task<void>
{
public:
    struct promise_type
    {
        using handle_type = std::experimental::coroutine_handle<promise_type>;

        task get_return_object() noexcept
        {
            return task(handle_type::from_promise(*this));
        }

        std::experimental::suspend_always initial_suspend() const noexcept
        {
            return {};
        }

        details::final_awaiter<void> final_suspend() const noexcept
        {
            return {};
        }

        void return_void() const noexcept
        {
        }

        void unhandled_exception() noexcept
        {
            exception_ = std::current_exception();
        }

        std::experimental::coroutine_handle<> continuation_;
        std::exception_ptr exception_;
    };

    using handle_type = std::experimental::coroutine_handle<promise_type>;

    task() noexcept
        : handle_(nullptr)
    {
    }

    explicit task(handle_type handle) noexcept
        : handle_(handle)
    {
    }

    task(task&& other) noexcept
        : handle_(other.handle_)
    {
        other.handle_ = nullptr;
    }

    task& operator=(task&& other) noexcept
    {
        if (this != &other)
        {
            if (handle_)
            {
                handle_.destroy();
            }

            handle_ = other.handle_;
            other.handle_ = nullptr;
        }

        return *this;
    }

    task(const task&) = delete;
    task& operator=(const task&) = delete;

    ~task()
    {
        if (handle_)
        {
            handle_.destroy();
        }
    }

    class awaiter
    {
    public:
        explicit awaiter(handle_type handle) noexcept
            : handle_(handle)
        {
        }

        awaiter(const awaiter&) = delete;
        awaiter& operator=(const awaiter&) = delete;

        awaiter(awaiter&& other) noexcept
            : handle_(other.handle_)
        {
            other.handle_ = nullptr;
        }

        awaiter& operator=(awaiter&& other) noexcept
        {
            if (this != &other)
            {
                if (handle_)
                {
                    handle_.destroy();
                }

                handle_ = other.handle_;
                other.handle_ = nullptr;
            }

            return *this;
        }

        ~awaiter()
        {
            if (handle_)
            {
                handle_.destroy();
            }
        }

        bool await_ready() const noexcept
        {
            return !handle_ || handle_.done();
        }

        void await_suspend(std::experimental::coroutine_handle<> continuation) noexcept
        {
            handle_.promise().continuation_ = continuation;
            handle_.resume();
        }

        void await_resume()
        {
            auto& promise = handle_.promise();
            if (promise.exception_)
            {
                std::rethrow_exception(promise.exception_);
            }
        }

    private:
        handle_type handle_;
    };

    awaiter operator co_await() && noexcept
    {
        auto handle = handle_;
        handle_ = nullptr;
        return awaiter(handle);
    }

private:
    handle_type handle_;
};

class fire_and_forget
{
public:
    struct promise_type
    {
        fire_and_forget get_return_object() const noexcept
        {
            return {};
        }

        std::experimental::suspend_never initial_suspend() const noexcept
        {
            return {};
        }

        struct final_awaiter
        {
            bool await_ready() const noexcept
            {
                return false;
            }

            template <typename Promise>
            void await_suspend(std::experimental::coroutine_handle<Promise> handle) const noexcept
            {
                handle.destroy();
            }

            void await_resume() const noexcept
            {
            }
        };

        final_awaiter final_suspend() const noexcept
        {
            return {};
        }

        void return_void() const noexcept
        {
        }

        void unhandled_exception() const noexcept
        {
            ::OutputDebugStringW(L"Unhandled exception in fire_and_forget coroutine.\n");
        }
    };
};

template <typename Callable>
using invoke_result_t = typename std::invoke_result<Callable>::type;

template <typename Callable>
task<invoke_result_t<Callable>> background_invoke_impl(Callable callable)
{
    using result_type = invoke_result_t<Callable>;
    std::exception_ptr exception;

    co_await resume_background();

    if constexpr (std::is_void<result_type>::value)
    {
        try
        {
            callable();
        }
        catch (...)
        {
            exception = std::current_exception();
        }

        co_await resume_ui();

        if (exception)
        {
            std::rethrow_exception(exception);
        }

        co_return;
    }
    else
    {
        std::optional<result_type> value;

        try
        {
            value = callable();
        }
        catch (...)
        {
            exception = std::current_exception();
        }

        co_await resume_ui();

        if (exception)
        {
            std::rethrow_exception(exception);
        }

        co_return std::move(*value);
    }
}

template <typename Callable>
task<invoke_result_t<typename std::decay<Callable>::type>> background_invoke(Callable&& callable)
{
    using callable_type = typename std::decay<Callable>::type;
    return background_invoke_impl<callable_type>(std::forward<Callable>(callable));
}

template <typename Object, typename Method, typename... Args>
auto background_invoke_member(Object&& object, Method method, Args&&... args)
    -> task<typename std::invoke_result<Method, details::decay_t<Object>&, details::decay_t<Args>...>::type>
{
    using object_type = details::decay_t<Object>;
    using arguments_type = std::tuple<details::decay_t<Args>...>;

    return background_invoke(
        [captured_object = object_type(std::forward<Object>(object)),
         captured_method = method,
         captured_arguments = arguments_type(details::decay_t<Args>(std::forward<Args>(args))...)]() mutable -> decltype(auto)
        {
            return details::invoke_member_from_tuple(
                captured_object,
                captured_method,
                captured_arguments,
                std::index_sequence_for<Args...>{});
        });
}

template <typename Object, typename Method, typename... Args>
auto background_invoke_shared_member(std::shared_ptr<Object> object, Method method, Args&&... args)
    -> task<typename std::invoke_result<Method, Object&, details::decay_t<Args>...>::type>
{
    using arguments_type = std::tuple<details::decay_t<Args>...>;

    return background_invoke(
        [captured_object = std::move(object),
         captured_method = method,
         captured_arguments = arguments_type(details::decay_t<Args>(std::forward<Args>(args))...)]() mutable -> decltype(auto)
        {
            return details::invoke_member_from_tuple(
                *captured_object,
                captured_method,
                captured_arguments,
                std::index_sequence_for<Args...>{});
        });
}

template <typename Result, typename Object, typename Method, typename... Args>
auto background_invoke_weak_member_impl(std::weak_ptr<Object> object, Method method, std::tuple<details::decay_t<Args>...> arguments, std::false_type)
    -> task<std::optional<Result>>
{
    std::exception_ptr exception;
    std::optional<Result> value;

    co_await resume_background();

    auto locked = object.lock();
    if (locked)
    {
        try
        {
            value = details::invoke_member_from_tuple(
                *locked,
                method,
                arguments,
                std::index_sequence_for<Args...>{});
        }
        catch (...)
        {
            exception = std::current_exception();
        }
    }

    co_await resume_ui();

    if (exception)
    {
        std::rethrow_exception(exception);
    }

    co_return value;
}

template <typename Result, typename Object, typename Method, typename... Args>
auto background_invoke_weak_member_impl(std::weak_ptr<Object> object, Method method, std::tuple<details::decay_t<Args>...> arguments, std::true_type)
    -> task<bool>
{
    std::exception_ptr exception;
    bool invoked = false;

    co_await resume_background();

    auto locked = object.lock();
    if (locked)
    {
        invoked = true;

        try
        {
            details::invoke_member_from_tuple(
                *locked,
                method,
                arguments,
                std::index_sequence_for<Args...>{});
        }
        catch (...)
        {
            exception = std::current_exception();
        }
    }

    co_await resume_ui();

    if (exception)
    {
        std::rethrow_exception(exception);
    }

    co_return invoked;
}

template <typename Object, typename Method, typename... Args>
auto background_invoke_weak_member(std::weak_ptr<Object> object, Method method, Args&&... args)
{
    using result_type = typename std::invoke_result<Method, Object&, details::decay_t<Args>...>::type;
    using arguments_type = std::tuple<details::decay_t<Args>...>;

    return background_invoke_weak_member_impl<result_type, Object, Method, Args...>(
        std::move(object),
        method,
        arguments_type(details::decay_t<Args>(std::forward<Args>(args))...),
        std::is_void<result_type>{});
}

template <typename Callable>
task<invoke_result_t<typename std::decay<Callable>::type>> AsyncCall(Callable&& callable)
{
    return background_invoke(std::forward<Callable>(callable));
}

template <typename Object, typename Method, typename... Args>
auto AsyncCall(Object&& object, Method method, Args&&... args)
    -> decltype(background_invoke_member(std::forward<Object>(object), method, std::forward<Args>(args)...))
{
    return background_invoke_member(std::forward<Object>(object), method, std::forward<Args>(args)...);
}

template <typename Object, typename Method, typename... Args>
auto AsyncCall(std::shared_ptr<Object> object, Method method, Args&&... args)
    -> decltype(background_invoke_shared_member(std::move(object), method, std::forward<Args>(args)...))
{
    return background_invoke_shared_member(std::move(object), method, std::forward<Args>(args)...);
}

template <typename Object, typename Method, typename... Args>
auto TryAsyncCall(std::weak_ptr<Object> object, Method method, Args&&... args)
    -> decltype(background_invoke_weak_member(std::move(object), method, std::forward<Args>(args)...))
{
    return background_invoke_weak_member(std::move(object), method, std::forward<Args>(args)...);
}
}

#define CPPV141_ASYNC(expr) ::cppv141async::background_invoke([&]() -> decltype(auto) { return (expr); })
#define CPPV141_ASYNC_MEMBER(type, object, method, ...) ::cppv141async::background_invoke_member((object), &type::method, __VA_ARGS__)
#define CPPV141_ASYNC_MEMBER_AUTO(object, method, ...) ::cppv141async::background_invoke_member((object), &::cppv141async::details::remove_cvref_t<decltype(object)>::method, __VA_ARGS__)
#define CPPV141_ASYNC_SHARED_MEMBER(object, method, ...) ::cppv141async::background_invoke_shared_member((object), &::cppv141async::details::remove_cvref_t<decltype(*(object))>::method, __VA_ARGS__)
#define CPPV141_ASYNC_WEAK_MEMBER(object, method, ...) ::cppv141async::background_invoke_weak_member((object), &::cppv141async::details::remove_cvref_t<decltype(*((object).lock()))>::method, __VA_ARGS__)