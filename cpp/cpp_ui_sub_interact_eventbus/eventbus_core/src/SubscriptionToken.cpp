#include "eventbus/SubscriptionToken.h"

#include <utility>

SubscriptionToken::SubscriptionToken() = default;

SubscriptionToken::SubscriptionToken(std::function<void()> releaseCallback)
    : m_releaseCallback(std::move(releaseCallback)) {
}

SubscriptionToken::SubscriptionToken(SubscriptionToken&& other) noexcept
    : m_releaseCallback(std::move(other.m_releaseCallback)) {
    other.m_releaseCallback = nullptr;
}

SubscriptionToken& SubscriptionToken::operator=(SubscriptionToken&& other) noexcept {
    if (this != &other) {
        reset();
        m_releaseCallback = std::move(other.m_releaseCallback);
        other.m_releaseCallback = nullptr;
    }

    return *this;
}

SubscriptionToken::~SubscriptionToken() {
    reset();
}

void SubscriptionToken::reset() {
    if (m_releaseCallback) {
        m_releaseCallback();
        m_releaseCallback = nullptr;
    }
}

SubscriptionToken::operator bool() const {
    return static_cast<bool>(m_releaseCallback);
}