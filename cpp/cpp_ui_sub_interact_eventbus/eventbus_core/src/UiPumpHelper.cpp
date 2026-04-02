#include "eventbus/UiPumpHelper.h"

int UiPumpHelper::runLoop(
    UiDispatcher& uiDispatcher,
    MessagePump pumpMessage,
    QuitHandler onQuit,
    WaitErrorHandler onWaitFailed,
    DWORD wakeMask) {
    if (!pumpMessage || !onQuit) {
        return 0;
    }

    const HANDLE wakeHandle = uiDispatcher.getWakeHandle();
    if (wakeHandle == nullptr) {
        if (onWaitFailed) {
            onWaitFailed(ERROR_INVALID_HANDLE);
        }

        return onQuit();
    }

    MSG msg = {};
    for (;;) {
        while (::PeekMessage(&msg, nullptr, 0, 0, PM_NOREMOVE)) {
            if (!pumpMessage()) {
                return onQuit();
            }
        }

        const DWORD waitResult = ::MsgWaitForMultipleObjects(
            1,
            &wakeHandle,
            FALSE,
            INFINITE,
            wakeMask);

        if (waitResult == WAIT_OBJECT_0) {
            uiDispatcher.drain();
            continue;
        }

        if (waitResult == WAIT_OBJECT_0 + 1) {
            continue;
        }

        if (onWaitFailed) {
            onWaitFailed(::GetLastError());
        }

        return onQuit();
    }
}