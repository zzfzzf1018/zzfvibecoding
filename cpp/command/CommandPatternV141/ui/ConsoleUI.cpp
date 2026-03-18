#include "ConsoleUI.h"

#include "../mgr/OrderCommands.h"

#include <iostream>

namespace ui {

ConsoleUI::ConsoleUI(mgr::CommandManager& manager)
    : manager_(manager) {
    manager_.SetExecutionListener([this](const mgr::CommandExecutionEvent& event) {
        OnCommandExecuted(event);
    });
}

void ConsoleUI::RunScriptedDemo() const {
    std::cout << "===== Scripted demo: Command Pattern drives ACT business logic =====" << std::endl;

    const std::vector<std::string> script = {
        "create O1001",
        "pay O1001",
        "ship O1001",
        "cancel O1001",
        "create O2001",
        "cancel O2001",
        "summary"
    };

    ExecuteBatch(script);
}

void ConsoleUI::RunInteractive() const {
    std::cout << std::endl;
    std::cout << "===== Interactive mode =====" << std::endl;
    std::cout << "Input commands: create/pay/ship/cancel/summary" << std::endl;
    std::cout << "Type 'exit' to quit." << std::endl;

    std::string line;
    while (true) {
        std::cout << "> ";
        if (!std::getline(std::cin, line)) {
            break;
        }
        if (line == "exit") {
            break;
        }
        manager_.Execute(line);
    }
}

void ConsoleUI::ExecuteBatch(const std::vector<std::string>& lines) const {
    for (std::size_t i = 0; i < lines.size(); ++i) {
        std::cout << "[UI] command(" << (i + 1U) << "): " << lines[i] << std::endl;
        manager_.Execute(lines[i]);
    }
}

void ConsoleUI::OnCommandExecuted(const mgr::CommandExecutionEvent& event) const {
    std::cout << "[UI] command done: " << event.commandName
              << ", success=" << (event.executeSuccess ? "true" : "false")
              << ", stateChanged=" << (event.stateChanged ? "true" : "false") << std::endl;

    if (event.stateChanged) {
        std::cout << "[UI] refresh triggered by command: " << event.rawCommandLine << std::endl;
    }
}

} // namespace ui
