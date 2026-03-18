#include "OrderActions.h"

#include <iostream>

namespace act {

bool OrderActionService::CreateOrder(const std::string& orderId) {
    if (Exists(orderId)) {
        std::cout << "[ACT] create failed, order already exists: " << orderId << std::endl;
        return false;
    }

    orders_[orderId] = OrderStatus::Created;
    std::cout << "[ACT] order created: " << orderId << std::endl;
    return true;
}

bool OrderActionService::PayOrder(const std::string& orderId) {
    auto it = orders_.find(orderId);
    if (it == orders_.end()) {
        std::cout << "[ACT] pay failed, order not found: " << orderId << std::endl;
        return false;
    }
    if (it->second != OrderStatus::Created) {
        std::cout << "[ACT] pay failed, current status is " << ToText(it->second) << std::endl;
        return false;
    }

    it->second = OrderStatus::Paid;
    std::cout << "[ACT] order paid: " << orderId << std::endl;
    return true;
}

bool OrderActionService::ShipOrder(const std::string& orderId) {
    auto it = orders_.find(orderId);
    if (it == orders_.end()) {
        std::cout << "[ACT] ship failed, order not found: " << orderId << std::endl;
        return false;
    }
    if (it->second != OrderStatus::Paid) {
        std::cout << "[ACT] ship failed, current status is " << ToText(it->second) << std::endl;
        return false;
    }

    it->second = OrderStatus::Shipped;
    std::cout << "[ACT] order shipped: " << orderId << std::endl;
    return true;
}

bool OrderActionService::CancelOrder(const std::string& orderId) {
    auto it = orders_.find(orderId);
    if (it == orders_.end()) {
        std::cout << "[ACT] cancel failed, order not found: " << orderId << std::endl;
        return false;
    }
    if (it->second == OrderStatus::Shipped) {
        std::cout << "[ACT] cancel failed, shipped order cannot be cancelled: " << orderId << std::endl;
        return false;
    }

    it->second = OrderStatus::Cancelled;
    std::cout << "[ACT] order cancelled: " << orderId << std::endl;
    return true;
}

void OrderActionService::PrintSummary() const {
    std::cout << "[ACT] order summary:" << std::endl;
    if (orders_.empty()) {
        std::cout << "  (none)" << std::endl;
        return;
    }

    for (const auto& pair : orders_) {
        std::cout << "  orderId=" << pair.first << ", status=" << ToText(pair.second) << std::endl;
    }
}

const char* OrderActionService::ToText(OrderStatus status) {
    switch (status) {
    case OrderStatus::Created:
        return "Created";
    case OrderStatus::Paid:
        return "Paid";
    case OrderStatus::Shipped:
        return "Shipped";
    case OrderStatus::Cancelled:
        return "Cancelled";
    default:
        return "Unknown";
    }
}

bool OrderActionService::Exists(const std::string& orderId) const {
    return orders_.find(orderId) != orders_.end();
}

} // namespace act
