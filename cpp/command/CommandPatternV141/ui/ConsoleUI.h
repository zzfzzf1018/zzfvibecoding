#pragma once

#include <string>
#include <vector>

namespace mgr {
class CommandManager;
struct CommandExecutionEvent;
}

namespace ui {

class ConsoleUI {
public:
    explicit ConsoleUI(mgr::CommandManager& manager);

    void RunScriptedDemo() const;
    void RunInteractive() const;

private:
    void ExecuteBatch(const std::vector<std::string>& lines) const;
    void OnCommandExecuted(const mgr::CommandExecutionEvent& event) const;

private:
    mgr::CommandManager& manager_;
};

} // namespace ui
