#pragma once

namespace mgr {

struct CommandResult {
    bool success;
    bool stateChanged;
};

class ICommand {
public:
    virtual ~ICommand() {}
    virtual CommandResult Execute() = 0;
};

} // namespace mgr
