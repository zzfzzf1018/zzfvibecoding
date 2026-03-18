#pragma once

#include <string>
#include <unordered_map>

namespace act {

class OrderActionService {
public:
    bool CreateOrder(const std::string& orderId);
    bool PayOrder(const std::string& orderId);
    bool ShipOrder(const std::string& orderId);
    bool CancelOrder(const std::string& orderId);
    void PrintSummary() const;

private:
    enum class OrderStatus {
        Created,
        Paid,
        Shipped,
        Cancelled
    };

    static const char* ToText(OrderStatus status);
    bool Exists(const std::string& orderId) const;

private:
    std::unordered_map<std::string, OrderStatus> orders_;
};

} // namespace act
