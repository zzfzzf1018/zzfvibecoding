#include <string>

#include <gtest/gtest.h>

#include "EventBus.h"

namespace {

struct LoginEvent : public IEvent {
    explicit LoginEvent(std::string name) : user(std::move(name)) {}
    std::string user;
};

class LoginCounterObserver : public IEventObserver<LoginEvent> {
public:
    void OnEvent(const LoginEvent& event) override {
        ++count;
        lastUser = event.user;
    }

    int count = 0;
    std::string lastUser;
};

class SecondaryLoginObserver : public IEventObserver<LoginEvent> {
public:
    void OnEvent(const LoginEvent&) override {
        ++count;
    }

    int count = 0;
};

TEST(EventBusTests, SubscribeAndPublish) {
    EventBus bus;
    LoginCounterObserver observer;

    bus.Subscribe<LoginEvent>(&observer);
    bus.Publish(LoginEvent("alice"));

    EXPECT_EQ(observer.count, 1);
    EXPECT_EQ(observer.lastUser, "alice");
}

TEST(EventBusTests, Unsubscribe) {
    EventBus bus;
    LoginCounterObserver observer;

    bus.Subscribe<LoginEvent>(&observer);
    bus.Unsubscribe<LoginEvent>(&observer);
    bus.Publish(LoginEvent("bob"));

    EXPECT_EQ(observer.count, 0);
}

TEST(EventBusTests, MultipleObservers) {
    EventBus bus;
    LoginCounterObserver first;
    SecondaryLoginObserver second;

    bus.Subscribe<LoginEvent>(&first);
    bus.Subscribe<LoginEvent>(&second);
    bus.Publish(LoginEvent("charlie"));

    EXPECT_EQ(first.count, 1);
    EXPECT_EQ(second.count, 1);
}

TEST(EventBusTests, NoDuplicateSubscribe) {
    EventBus bus;
    LoginCounterObserver observer;

    bus.Subscribe<LoginEvent>(&observer);
    bus.Subscribe<LoginEvent>(&observer);
    bus.Publish(LoginEvent("david"));

    EXPECT_EQ(observer.count, 1);
}

} // namespace

int main(int argc, char** argv) {
    ::testing::InitGoogleTest(&argc, argv);
    return RUN_ALL_TESTS();
}
