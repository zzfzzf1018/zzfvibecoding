#pragma once

#include <cstddef>
#include <cstdint>
#include <string>

#include "EventBus.h"

namespace eb {

struct CommandPublishLoginEvent {
    std::string user;
};

struct CommandSetPolicyEvent {
    AsyncQueuePolicy policy;
    std::size_t queueSize;
};

struct CommandRunBurstEvent {
    bool coalesced;
    int count;
};

struct CommandToggleUiExecutorEvent {
};

struct CommandResetCountersEvent {
};

struct CommandPublishSyncTickEvent {
};

class CommandEventBus : public EventBus {
public:
    PublishStatus PublishCommandPublishLogin(const std::string& user) {
        return PublishSync(CommandPublishLoginEvent{user});
    }

    PublishStatus PublishCommandSetPolicy(AsyncQueuePolicy policy, std::size_t queueSize) {
        return PublishSync(CommandSetPolicyEvent{policy, queueSize});
    }

    PublishStatus PublishCommandRunBurst(bool coalesced, int count) {
        return PublishSync(CommandRunBurstEvent{coalesced, count});
    }

    PublishStatus PublishCommandToggleUiExecutor() {
        return PublishSync(CommandToggleUiExecutorEvent{});
    }

    PublishStatus PublishCommandResetCounters() {
        return PublishSync(CommandResetCountersEvent{});
    }

    PublishStatus PublishCommandPublishSyncTick() {
        return PublishSync(CommandPublishSyncTickEvent{});
    }

    template <typename Obj>
    std::uint64_t SubscribeCommandPublishLogin(
        Obj* obj,
        void (Obj::*method)(const CommandPublishLoginEvent&),
        RegistrationRule rule = RegistrationRule::OneToMany) {
        return Subscribe<CommandPublishLoginEvent>(obj, method, rule, DispatchTarget::CurrentThread);
    }

    template <typename Obj>
    std::uint64_t SubscribeCommandSetPolicy(
        Obj* obj,
        void (Obj::*method)(const CommandSetPolicyEvent&),
        RegistrationRule rule = RegistrationRule::OneToMany) {
        return Subscribe<CommandSetPolicyEvent>(obj, method, rule, DispatchTarget::CurrentThread);
    }

    template <typename Obj>
    std::uint64_t SubscribeCommandRunBurst(
        Obj* obj,
        void (Obj::*method)(const CommandRunBurstEvent&),
        RegistrationRule rule = RegistrationRule::OneToMany) {
        return Subscribe<CommandRunBurstEvent>(obj, method, rule, DispatchTarget::CurrentThread);
    }

    template <typename Obj>
    std::uint64_t SubscribeCommandToggleUiExecutor(
        Obj* obj,
        void (Obj::*method)(const CommandToggleUiExecutorEvent&),
        RegistrationRule rule = RegistrationRule::OneToMany) {
        return Subscribe<CommandToggleUiExecutorEvent>(obj, method, rule, DispatchTarget::CurrentThread);
    }

    template <typename Obj>
    std::uint64_t SubscribeCommandResetCounters(
        Obj* obj,
        void (Obj::*method)(const CommandResetCountersEvent&),
        RegistrationRule rule = RegistrationRule::OneToMany) {
        return Subscribe<CommandResetCountersEvent>(obj, method, rule, DispatchTarget::CurrentThread);
    }

    template <typename Obj>
    std::uint64_t SubscribeCommandPublishSyncTick(
        Obj* obj,
        void (Obj::*method)(const CommandPublishSyncTickEvent&),
        RegistrationRule rule = RegistrationRule::OneToMany) {
        return Subscribe<CommandPublishSyncTickEvent>(obj, method, rule, DispatchTarget::CurrentThread);
    }
};

}  // namespace eb
