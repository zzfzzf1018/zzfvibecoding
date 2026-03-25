#pragma once

#include <afxwin.h>

#include <atomic>
#include <chrono>
#include <exception>
#include <functional>
#include <mutex>
#include <memory>
#include <optional>
#include <queue>
#include <thread>
#include <type_traits>
#include <utility>

namespace async_mfc {

constexpr UINT WM_ASYNC_CONTINUATION = WM_APP + 100;

class UiContinuationQueue {
public:
    UiContinuationQueue() = default;

    void Attach(HWND hwnd) {
        m_hwnd = hwnd;
    }

    void Post(std::function<void()> continuation) {
        {
            std::lock_guard<std::mutex> lock(m_mutex);
            m_queue.push(std::move(continuation));
        }

        if (m_hwnd != nullptr) {
            ::PostMessage(m_hwnd, WM_ASYNC_CONTINUATION, 0, 0);
        }
    }

    void Drain() {
        std::queue<std::function<void()>> local;

        {
            std::lock_guard<std::mutex> lock(m_mutex);
            std::swap(local, m_queue);
        }

        while (!local.empty()) {
            auto fn = std::move(local.front());
            local.pop();

            if (fn) {
                fn();
            }
        }
    }

private:
    HWND m_hwnd = nullptr;
    std::mutex m_mutex;
    std::queue<std::function<void()>> m_queue;
};

class CancellationToken {
public:
    CancellationToken() : m_state(std::make_shared<std::atomic_bool>(false)) {}

    explicit CancellationToken(std::shared_ptr<std::atomic_bool> state)
        : m_state(std::move(state)) {}

    bool IsCancellationRequested() const {
        return m_state && m_state->load(std::memory_order_relaxed);
    }

private:
    std::shared_ptr<std::atomic_bool> m_state;
};

class CancellationTokenSource {
public:
    CancellationTokenSource() : m_state(std::make_shared<std::atomic_bool>(false)) {}

    CancellationToken Token() const {
        return CancellationToken(m_state);
    }

    void Cancel() {
        m_state->store(true, std::memory_order_relaxed);
    }

private:
    std::shared_ptr<std::atomic_bool> m_state;
};

class OperationCanceledException : public std::exception {
public:
    const char* what() const noexcept override {
        return "operation canceled";
    }
};

inline void ThrowIfCancellationRequested(const CancellationToken& token) {
    if (token.IsCancellationRequested()) {
        throw OperationCanceledException();
    }
}

inline bool IsCanceledException(std::exception_ptr ep) {
    if (!ep) {
        return false;
    }

    try {
        std::rethrow_exception(ep);
    } catch (const OperationCanceledException&) {
        return true;
    } catch (...) {
        return false;
    }
}

template <typename Work, typename Continuation>
std::enable_if_t<!std::is_void_v<std::invoke_result_t<Work>>, void>
Await(std::shared_ptr<UiContinuationQueue> uiQueue, Work&& work, Continuation&& continuation) {
    using Result = std::invoke_result_t<Work>;

    auto workFn = std::forward<Work>(work);
    auto continuationFn = std::forward<Continuation>(continuation);

    std::thread([
        uiQueue,
        work = std::move(workFn),
        continuation = std::move(continuationFn)]() mutable {
        try {
            Result result = work();
            uiQueue->Post([continuation = std::move(continuation),
                           result = std::move(result)]() mutable {
                continuation(std::optional<Result>(std::move(result)), nullptr);
            });
        } catch (...) {
            auto ep = std::current_exception();
            uiQueue->Post([continuation = std::move(continuation), ep]() mutable {
                continuation(std::optional<Result>(), ep);
            });
        }
    }).detach();
}

template <typename Work, typename ProgressCallback, typename Continuation>
std::enable_if_t<!std::is_void_v<std::invoke_result_t<Work, const CancellationToken&, const std::function<void(int)>&>>, void>
AwaitCancellableProgress(std::shared_ptr<UiContinuationQueue> uiQueue,
                         CancellationToken token,
                         Work&& work,
                         ProgressCallback&& progressCallback,
                         Continuation&& continuation) {
    using Result = std::invoke_result_t<Work, const CancellationToken&, const std::function<void(int)>&>;

    auto workFn = std::forward<Work>(work);
    auto progressFn = std::make_shared<std::decay_t<ProgressCallback>>(std::forward<ProgressCallback>(progressCallback));
    auto continuationFn = std::forward<Continuation>(continuation);

    std::thread([
        uiQueue,
        token,
        work = std::move(workFn),
        progress = std::move(progressFn),
        continuation = std::move(continuationFn)]() mutable {
        auto reportProgress = [uiQueue, progress](int percent) {
            int clamped = percent;
            if (clamped < 0) {
                clamped = 0;
            } else if (clamped > 100) {
                clamped = 100;
            }
            uiQueue->Post([progress, clamped]() {
                (*progress)(clamped);
            });
        };

        try {
            Result result = work(token, reportProgress);
            uiQueue->Post([continuation = std::move(continuation),
                           result = std::move(result)]() mutable {
                continuation(std::optional<Result>(std::move(result)), nullptr);
            });
        } catch (...) {
            auto ep = std::current_exception();
            uiQueue->Post([continuation = std::move(continuation), ep]() mutable {
                continuation(std::optional<Result>(), ep);
            });
        }
    }).detach();
}

template <typename Work, typename Continuation>
std::enable_if_t<std::is_void_v<std::invoke_result_t<Work>>, void>
Await(std::shared_ptr<UiContinuationQueue> uiQueue, Work&& work, Continuation&& continuation) {
    auto workFn = std::forward<Work>(work);
    auto continuationFn = std::forward<Continuation>(continuation);

    std::thread([
        uiQueue,
        work = std::move(workFn),
        continuation = std::move(continuationFn)]() mutable {
        try {
            work();
            uiQueue->Post([continuation = std::move(continuation)]() mutable {
                continuation(nullptr);
            });
        } catch (...) {
            auto ep = std::current_exception();
            uiQueue->Post([continuation = std::move(continuation), ep]() mutable {
                continuation(ep);
            });
        }
    }).detach();
}

inline CString NowTimeText() {
    CTime now = CTime::GetCurrentTime();
    return now.Format(_T("%H:%M:%S"));
}

} // namespace async_mfc
