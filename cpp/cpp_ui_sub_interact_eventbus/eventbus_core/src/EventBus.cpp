#include "eventbus/EventBus.h"

#include <algorithm>
#include <utility>

EventBus::EventBus(UiDispatcher& uiDispatcher)
    : m_uiDispatcher(uiDispatcher),
            m_nextHandlerId(1) {
}

SubscriptionToken EventBus::subscribeRaw(
    std::type_index eventType,
    DispatchPolicy policy,
    std::function<void(const void*)> handler) {
    const std::uint64_t handlerId = m_nextHandlerId.fetch_add(1, std::memory_order_relaxed);

    HandlerEntry entry;
    entry.m_id = handlerId;
    entry.m_dispatchPolicy = policy;
    entry.m_handler = std::move(handler);

    {
        std::lock_guard<std::mutex> lock(m_routesMutex);
        m_routes[eventType].push_back(std::move(entry));
    }

    return SubscriptionToken([this, eventType, handlerId]() {
        unsubscribe(eventType, handlerId);
    });
}

    void EventBus::publishRaw(
    std::type_index eventType,
    std::shared_ptr<const void> eventHolder) {
    std::vector<HandlerEntry> subscribers;
    {
        std::lock_guard<std::mutex> lock(m_routesMutex);
        const auto found = m_routes.find(eventType);
        if (found == m_routes.end()) {
            return;
        }

        subscribers = found->second;
    }

    const void* rawEvent = eventHolder.get();
    for (const auto& subscriber : subscribers) {
        if (subscriber.m_dispatchPolicy == DispatchPolicy::UiThread && !m_uiDispatcher.isUiThread()) {
            auto handler = subscriber.m_handler;
            m_uiDispatcher.post([handler, eventHolder, rawEvent]() {
                handler(rawEvent);
            });
            continue;
        }

        subscriber.m_handler(rawEvent);
    }
}

void EventBus::unsubscribe(std::type_index eventType, std::uint64_t handlerId) {
    std::lock_guard<std::mutex> lock(m_routesMutex);
    const auto found = m_routes.find(eventType);
    if (found == m_routes.end()) {
        return;
    }

    auto& handlers = found->second;
    handlers.erase(std::remove_if(handlers.begin(), handlers.end(), [handlerId](const HandlerEntry& entry) {
        return entry.m_id == handlerId;
    }), handlers.end());

    if (handlers.empty()) {
        m_routes.erase(found);
    }
}