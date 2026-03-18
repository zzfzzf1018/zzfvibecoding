#include "OrderCommands.h"

#include "../act/OrderActions.h"
#include "Command.h"

#include <iostream>
#include <sstream>
#include <utility>

namespace mgr {

namespace {

class CreateOrderCommand : public ICommand {
public:
    CreateOrderCommand(act::OrderActionService& actionService, std::string orderId)
        : actionService_(actionService), orderId_(std::move(orderId)) {}

    CommandResult Execute() override {
        const bool changed = actionService_.CreateOrder(orderId_);
        return CommandResult{changed, changed};
    }

private:
    act::OrderActionService& actionService_;
    std::string orderId_;
};

class PayOrderCommand : public ICommand {
public:
    PayOrderCommand(act::OrderActionService& actionService, std::string orderId)
        : actionService_(actionService), orderId_(std::move(orderId)) {}

    CommandResult Execute() override {
        const bool changed = actionService_.PayOrder(orderId_);
        return CommandResult{changed, changed};
    }

private:
    act::OrderActionService& actionService_;
    std::string orderId_;
};

class ShipOrderCommand : public ICommand {
public:
    ShipOrderCommand(act::OrderActionService& actionService, std::string orderId)
        : actionService_(actionService), orderId_(std::move(orderId)) {}

    CommandResult Execute() override {
        const bool changed = actionService_.ShipOrder(orderId_);
        return CommandResult{changed, changed};
    }

private:
    act::OrderActionService& actionService_;
    std::string orderId_;
};

class CancelOrderCommand : public ICommand {
public:
    CancelOrderCommand(act::OrderActionService& actionService, std::string orderId)
        : actionService_(actionService), orderId_(std::move(orderId)) {}

    CommandResult Execute() override {
        const bool changed = actionService_.CancelOrder(orderId_);
        return CommandResult{changed, changed};
    }

private:
    act::OrderActionService& actionService_;
    std::string orderId_;
};

class SummaryCommand : public ICommand {
public:
    explicit SummaryCommand(act::OrderActionService& actionService)
        : actionService_(actionService) {}

    CommandResult Execute() override {
        actionService_.PrintSummary();
        return CommandResult{true, false};
    }

private:
    act::OrderActionService& actionService_;
};

class InvalidCommand : public ICommand {
public:
    explicit InvalidCommand(std::string reason)
        : reason_(std::move(reason)) {}

    CommandResult Execute() override {
        std::cout << "[MGR] invalid command: " << reason_ << std::endl;
        return CommandResult{false, false};
    }

private:
    std::string reason_;
};

} // namespace

void CommandManager::Register(const std::string& commandName, CommandFactory factory) {
    factories_[commandName] = std::move(factory);
}

void CommandManager::SetExecutionListener(ExecutionListener listener) {
    listener_ = std::move(listener);
}

bool CommandManager::Execute(const std::string& commandLine) {
    const std::vector<std::string> tokens = Tokenize(commandLine);
    if (tokens.empty()) {
        return false;
    }

    const std::string commandName = tokens[0];
    std::vector<std::string> args(tokens.begin() + 1, tokens.end());

    const std::unordered_map<std::string, CommandFactory>::const_iterator it = factories_.find(commandName);
    if (it == factories_.end()) {
        std::cout << "[MGR] command not found: " << commandName << std::endl;
        if (listener_) {
            listener_(CommandExecutionEvent{commandLine, commandName, false, false});
        }
        return false;
    }

    std::unique_ptr<ICommand> command = it->second(args);
    const CommandResult result = command->Execute();
    if (listener_) {
        listener_(CommandExecutionEvent{commandLine, commandName, result.success, result.stateChanged});
    }
    return result.success;
}

std::vector<std::string> CommandManager::Tokenize(const std::string& commandLine) {
    std::vector<std::string> tokens;
    std::istringstream stream(commandLine);
    std::string token;

    while (stream >> token) {
        tokens.push_back(token);
    }

    return tokens;
}

void RegisterOrderCommands(CommandManager& manager, act::OrderActionService& actionService) {
    manager.Register("create", [&actionService](const std::vector<std::string>& args) -> std::unique_ptr<ICommand> {
        if (args.size() != 1U) {
            return std::unique_ptr<ICommand>(new InvalidCommand("usage: create <orderId>"));
        }
        return std::unique_ptr<ICommand>(new CreateOrderCommand(actionService, args[0]));
    });

    manager.Register("pay", [&actionService](const std::vector<std::string>& args) -> std::unique_ptr<ICommand> {
        if (args.size() != 1U) {
            return std::unique_ptr<ICommand>(new InvalidCommand("usage: pay <orderId>"));
        }
        return std::unique_ptr<ICommand>(new PayOrderCommand(actionService, args[0]));
    });

    manager.Register("ship", [&actionService](const std::vector<std::string>& args) -> std::unique_ptr<ICommand> {
        if (args.size() != 1U) {
            return std::unique_ptr<ICommand>(new InvalidCommand("usage: ship <orderId>"));
        }
        return std::unique_ptr<ICommand>(new ShipOrderCommand(actionService, args[0]));
    });

    manager.Register("cancel", [&actionService](const std::vector<std::string>& args) -> std::unique_ptr<ICommand> {
        if (args.size() != 1U) {
            return std::unique_ptr<ICommand>(new InvalidCommand("usage: cancel <orderId>"));
        }
        return std::unique_ptr<ICommand>(new CancelOrderCommand(actionService, args[0]));
    });

    manager.Register("summary", [&actionService](const std::vector<std::string>& args) -> std::unique_ptr<ICommand> {
        if (!args.empty()) {
            return std::unique_ptr<ICommand>(new InvalidCommand("usage: summary"));
        }
        return std::unique_ptr<ICommand>(new SummaryCommand(actionService));
    });
}

} // namespace mgr
