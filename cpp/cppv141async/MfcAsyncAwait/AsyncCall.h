#pragma once

#include "Dispatcher.h"
#include "Task.h"

// Header-only by design: AsyncCall/TryAsyncCall are template APIs and need to
// stay visible at the call site for arbitrary object and argument types.

#include <functional>
#include <memory>
#include <tuple>
#include <type_traits>
#include <utility>

namespace cppv141async
{
namespace details
{
template <typename T>
using decay_t = typename std::decay<T>::type;

template <typename T>
using remove_cvref_t = typename std::remove_cv<typename std::remove_reference<T>::type>::type;

template <typename Callable>
using invoke_result_t = typename std::invoke_result<Callable>::type;

template <typename Object, typename Method, typename Tuple, size_t... Indices>
auto invoke_member_from_tuple(Object& object, Method method, Tuple& arguments, std::index_sequence<Indices...>)
    -> typename std::invoke_result<Method, Object&, decay_t<decltype(std::get<Indices>(arguments))>...>::type
{
    return std::mem_fn(method)(object, std::get<Indices>(arguments)...);
}

template <typename Callable>
task<invoke_result_t<Callable>> run_background_impl(Callable callable)
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

        co_return extract_result(*value);
    }
}

template <typename Callable>
task<invoke_result_t<typename std::decay<Callable>::type>> run_background(Callable&& callable)
{
    using callable_type = typename std::decay<Callable>::type;
    return run_background_impl<callable_type>(std::forward<Callable>(callable));
}

template <typename Object, typename Method, typename... Args>
auto run_member(Object&& object, Method method, Args&&... args)
    -> task<typename std::invoke_result<Method, decay_t<Object>&, decay_t<Args>...>::type>
{
    using object_type = decay_t<Object>;
    using arguments_type = std::tuple<decay_t<Args>...>;

    return run_background(
        [captured_object = object_type(std::forward<Object>(object)),
         captured_method = method,
         captured_arguments = arguments_type(decay_t<Args>(std::forward<Args>(args))...)]() mutable -> decltype(auto)
        {
            return invoke_member_from_tuple(
                captured_object,
                captured_method,
                captured_arguments,
                std::index_sequence_for<Args...>{});
        });
}

template <typename Object, typename Method, typename... Args>
auto run_shared_member(std::shared_ptr<Object> object, Method method, Args&&... args)
    -> task<typename std::invoke_result<Method, Object&, decay_t<Args>...>::type>
{
    using arguments_type = std::tuple<decay_t<Args>...>;

    return run_background(
        [captured_object = std::move(object),
         captured_method = method,
         captured_arguments = arguments_type(decay_t<Args>(std::forward<Args>(args))...)]() mutable -> decltype(auto)
        {
            return invoke_member_from_tuple(
                *captured_object,
                captured_method,
                captured_arguments,
                std::index_sequence_for<Args...>{});
        });
}

template <typename Result, typename Object, typename Method, typename... Args>
auto try_weak_member_impl(std::weak_ptr<Object> object, Method method, std::tuple<decay_t<Args>...> arguments, std::false_type)
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
            value = invoke_member_from_tuple(
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
auto try_weak_member_impl(std::weak_ptr<Object> object, Method method, std::tuple<decay_t<Args>...> arguments, std::true_type)
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
            invoke_member_from_tuple(
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
auto try_weak_member(std::weak_ptr<Object> object, Method method, Args&&... args)
{
    using result_type = typename std::invoke_result<Method, Object&, decay_t<Args>...>::type;
    using arguments_type = std::tuple<decay_t<Args>...>;

    return try_weak_member_impl<result_type, Object, Method, Args...>(
        std::move(object),
        method,
        arguments_type(decay_t<Args>(std::forward<Args>(args))...),
        std::is_void<result_type>{});
}
}

template <typename Callable>
task<details::invoke_result_t<typename std::decay<Callable>::type>> AsyncCall(Callable&& callable)
{
    return details::run_background(std::forward<Callable>(callable));
}

template <typename Object, typename Method, typename... Args>
auto AsyncCall(Object&& object, Method method, Args&&... args)
    -> decltype(details::run_member(std::forward<Object>(object), method, std::forward<Args>(args)...))
{
    return details::run_member(std::forward<Object>(object), method, std::forward<Args>(args)...);
}

template <typename Object, typename Method, typename... Args>
auto AsyncCall(std::shared_ptr<Object> object, Method method, Args&&... args)
    -> decltype(details::run_shared_member(std::move(object), method, std::forward<Args>(args)...))
{
    return details::run_shared_member(std::move(object), method, std::forward<Args>(args)...);
}

template <typename Object, typename Method, typename... Args>
auto TryAsyncCall(std::weak_ptr<Object> object, Method method, Args&&... args)
    -> decltype(details::try_weak_member(std::move(object), method, std::forward<Args>(args)...))
{
    return details::try_weak_member(std::move(object), method, std::forward<Args>(args)...);
}
}