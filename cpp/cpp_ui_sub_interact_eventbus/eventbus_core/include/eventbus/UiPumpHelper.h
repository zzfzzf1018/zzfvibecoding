#pragma once

#include <Windows.h>

#include <functional>

#include "eventbus/UiDispatcher.h"

// Helper for hosts that own their own message loop and want to wait on both
// normal window messages and the dispatcher's event handle.
class UiPumpHelper {
public:
    // Pumps one host-specific message step. Returns false when the host loop
    // should terminate.
    using MessagePump = std::function<bool()>;

    // Called when the loop exits and should return the process exit code.
    using QuitHandler = std::function<int()>;

    // Optional callback for wait failures.
    using WaitErrorHandler = std::function<void(DWORD)>;

    // Runs a custom UI loop that waits on both the host message queue and the
    // dispatcher's event handle. This is intended for hosts that already own
    // their main loop rather than default MFC message pumping.
    static int runLoop(
        UiDispatcher& uiDispatcher,
        MessagePump pumpMessage,
        QuitHandler onQuit,
        WaitErrorHandler onWaitFailed = WaitErrorHandler(),
        DWORD wakeMask = QS_ALLINPUT);
};