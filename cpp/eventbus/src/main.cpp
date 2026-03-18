#include <iostream>
#include <string>

#include "EventBus.h"

class UserLoginEvent : public IEvent {
public:
    explicit UserLoginEvent(std::string username) : username_(std::move(username)) {}

    const std::string& Username() const {
        return username_;
    }

private:
    std::string username_;
};

class OrderPaidEvent : public IEvent {
public:
    OrderPaidEvent(int orderId, double amount) : orderId_(orderId), amount_(amount) {}

    int OrderId() const {
        return orderId_;
    }

    double Amount() const {
        return amount_;
    }

private:
    int orderId_;
    double amount_;
};

class ConsoleLogger : public IEventObserver<UserLoginEvent>, public IEventObserver<OrderPaidEvent> {
public:
    void OnEvent(const UserLoginEvent& event) override {
        std::cout << "[Logger] User login: " << event.Username() << std::endl;
    }

    void OnEvent(const OrderPaidEvent& event) override {
        std::cout << "[Logger] Order paid: id=" << event.OrderId() << ", amount=" << event.Amount() << std::endl;
    }
};

class AnalyticsObserver : public IEventObserver<UserLoginEvent> {
public:
    void OnEvent(const UserLoginEvent& event) override {
        std::cout << "[Analytics] login tracked for: " << event.Username() << std::endl;
    }
};

int main() {
    EventBus eventBus;
    ConsoleLogger logger;
    AnalyticsObserver analytics;

    eventBus.Subscribe<UserLoginEvent>(&logger);
    eventBus.Subscribe<UserLoginEvent>(&analytics);
    eventBus.Subscribe<OrderPaidEvent>(&logger);

    UserLoginEvent login("alice");
    OrderPaidEvent paid(1001, 299.5);

    eventBus.Publish(login);
    eventBus.Publish(paid);

    eventBus.Unsubscribe<UserLoginEvent>(&analytics);
    std::cout << "---- after unsubscribe analytics from UserLoginEvent ----" << std::endl;

    UserLoginEvent loginAgain("bob");
    eventBus.Publish(loginAgain);

    return 0;
}
