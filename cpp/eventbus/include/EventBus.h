#pragma once

#include <algorithm>
#include <functional>
#include <mutex>
#include <typeindex>
#include <unordered_map>
#include <vector>

class IEvent {
public:
    virtual ~IEvent() = default;
};

template <typename TEvent>
class IEventObserver {
public:
    virtual ~IEventObserver() = default;
    virtual void OnEvent(const TEvent& event) = 0;
};

class EventBus {
public:
    template <typename TEvent>
    void Subscribe(IEventObserver<TEvent>* observer) {
        if (observer == nullptr) {
            return;
        }

        const std::type_index eventType = std::type_index(typeid(TEvent));
        const void* observerKey = static_cast<const void*>(observer);

        std::lock_guard<std::mutex> lock(mutex_);
        auto& slots = observersByType_[eventType];

        for (const ObserverSlot& slot : slots) {
            if (slot.observerKey == observerKey) {
                return;
            }
        }

        slots.push_back(ObserverSlot{
            observerKey,
            [observer](const void* eventData) {
                observer->OnEvent(*static_cast<const TEvent*>(eventData));
            }
        });
    }

    template <typename TEvent>
    void Unsubscribe(IEventObserver<TEvent>* observer) {
        if (observer == nullptr) {
            return;
        }

        const std::type_index eventType = std::type_index(typeid(TEvent));
        const void* observerKey = static_cast<const void*>(observer);

        std::lock_guard<std::mutex> lock(mutex_);
        auto it = observersByType_.find(eventType);
        if (it == observersByType_.end()) {
            return;
        }

        auto& slots = it->second;
        slots.erase(
            std::remove_if(
                slots.begin(),
                slots.end(),
                [observerKey](const ObserverSlot& slot) {
                    return slot.observerKey == observerKey;
                }),
            slots.end());

        if (slots.empty()) {
            observersByType_.erase(it);
        }
    }

    template <typename TEvent>
    void Publish(const TEvent& event) const {
        std::vector<ObserverSlot> snapshot;
        const std::type_index eventType = std::type_index(typeid(TEvent));

        {
            std::lock_guard<std::mutex> lock(mutex_);
            auto it = observersByType_.find(eventType);
            if (it == observersByType_.end()) {
                return;
            }
            snapshot = it->second;
        }

        const void* eventData = static_cast<const void*>(&event);
        for (const ObserverSlot& slot : snapshot) {
            slot.invoke(eventData);
        }
    }

private:
    struct ObserverSlot {
        const void* observerKey;
        std::function<void(const void*)> invoke;
    };

    mutable std::mutex mutex_;
    std::unordered_map<std::type_index, std::vector<ObserverSlot>> observersByType_;
};
