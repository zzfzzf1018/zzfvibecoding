#pragma once

#include <atomic>
#include <cstdint>
#include <functional>
#include <memory>
#include <mutex>
#include <typeindex>
#include <unordered_map>
#include <vector>

#include "eventbus/SubscriptionToken.h"
#include "eventbus/UiDispatcher.h"

enum class DispatchPolicy {
    // Invoke handlers immediately on the publishing thread.
    Inline,

    // Marshal handlers to the UI thread through UiDispatcher when publishing
    // from a non-UI thread.
    UiThread,
};

// A type-indexed in-process event bus. It can either invoke handlers inline on
// the publishing thread or marshal them back to the UI thread through
// UiDispatcher.
class EventBus {
public:
    explicit EventBus(UiDispatcher& uiDispatcher);
    ~EventBus() = default;

    EventBus(const EventBus&) = delete;
    EventBus& operator=(const EventBus&) = delete;

    // Subscribes a typed handler. The returned token owns the subscription and
    // should remain alive for as long as the handler is valid.
    template <typename TEvent>
    SubscriptionToken subscribe(std::function<void(const TEvent&)> handler, DispatchPolicy policy) {
        return subscribeRaw(
            std::type_index(typeid(TEvent)),
            policy,
            [handler = std::move(handler)](const void* rawEvent) {
                handler(*static_cast<const TEvent*>(rawEvent));
            });
    }

    // Publishes a typed event to all handlers currently subscribed for that
    // event type.
    template <typename TEvent>
    void publish(const TEvent& event) {
        std::shared_ptr<const void> eventHolder = std::make_shared<TEvent>(event);
        publishRaw(std::type_index(typeid(TEvent)), std::move(eventHolder));
    }

private:
    struct HandlerEntry {
        std::uint64_t m_id = 0;
        DispatchPolicy m_dispatchPolicy = DispatchPolicy::Inline;
        std::function<void(const void*)> m_handler;
    };

    SubscriptionToken subscribeRaw(
        std::type_index eventType,
        DispatchPolicy policy,
        std::function<void(const void*)> handler);

    void publishRaw(
        std::type_index eventType,
        std::shared_ptr<const void> eventHolder);

    void unsubscribe(std::type_index eventType, std::uint64_t handlerId);

    UiDispatcher& m_uiDispatcher;
    std::atomic<std::uint64_t> m_nextHandlerId;
    std::mutex m_routesMutex;
    std::unordered_map<std::type_index, std::vector<HandlerEntry>> m_routes;
};