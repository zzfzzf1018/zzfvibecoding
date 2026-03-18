#include "act/OrderActions.h"
#include "mgr/OrderCommands.h"
#include "ui/ConsoleUI.h"

int main() {
    act::OrderActionService actionService;

    mgr::CommandManager manager;
    mgr::RegisterOrderCommands(manager, actionService);

    ui::ConsoleUI console(manager);
    console.RunScriptedDemo();
    console.RunInteractive();

    return 0;
}
