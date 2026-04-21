#pragma once

#include <exception>
#include <experimental/coroutine>
#include <optional>
#include <type_traits>
#include <utility>

namespace cppv141async
{
template <typename T>
class task;

namespace details
{
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

// This task<T> is most stable for copyable business result types such as CString,
// std::wstring, and value objects. Move-only result types can still work, but
// they depend on stricter lifetime and transfer assumptions across coroutine frames.
template <typename T>
T extract_result(T& value)
{
    if constexpr (std::is_copy_constructible<T>::value)
    {
        return value;
    }
    else
    {
        return std::move(value);
    }
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

            return details::extract_result(*promise.value_);
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
}