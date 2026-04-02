#pragma once

#include <functional>

// Owns a single subscription lifetime. Destroying or resetting the token
// unsubscribes the corresponding handler.
class SubscriptionToken {
public:
    // Creates an empty token that does not own a subscription.
    SubscriptionToken();

    // Creates a token that releases a subscription when reset() or the
    // destructor is called.
    explicit SubscriptionToken(std::function<void()> releaseCallback);

    SubscriptionToken(const SubscriptionToken&) = delete;
    SubscriptionToken& operator=(const SubscriptionToken&) = delete;

    // Transfers ownership of the underlying unsubscribe callback.
    SubscriptionToken(SubscriptionToken&& other) noexcept;
    SubscriptionToken& operator=(SubscriptionToken&& other) noexcept;

    ~SubscriptionToken();

    // Releases the owned subscription, if any.
    void reset();

    // Returns true when the token still owns a live release callback.
    explicit operator bool() const;

private:
    std::function<void()> m_releaseCallback;
};