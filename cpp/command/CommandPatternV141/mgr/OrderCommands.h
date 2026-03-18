#pragma once

#include <functional>
#include <memory>
#include <string>
#include <unordered_map>
#include <vector>

namespace act {
class OrderActionService;
}

namespace mgr {

class ICommand;

struct CommandExecutionEvent {
    std::string rawCommandLine;
    std::string commandName;
    bool executeSuccess;
    bool stateChanged;
};

class CommandManager {
public:
    typedef std::function<std::unique_ptr<ICommand>(const std::vector<std::string>& args)> CommandFactory;
    typedef std::function<void(const CommandExecutionEvent& event)> ExecutionListener;

    void Register(const std::string& commandName, CommandFactory factory);
    void SetExecutionListener(ExecutionListener listener);
    bool Execute(const std::string& commandLine);

private:
    static std::vector<std::string> Tokenize(const std::string& commandLine);

private:
    std::unordered_map<std::string, CommandFactory> factories_;
    ExecutionListener listener_;
};

void RegisterOrderCommands(CommandManager& manager, act::OrderActionService& actionService);

} // namespace mgr
