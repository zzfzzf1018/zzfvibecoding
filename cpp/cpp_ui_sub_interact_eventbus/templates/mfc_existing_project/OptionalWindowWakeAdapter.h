#pragma once

#include <afxwin.h>
#include <Windows.h>

#include "AppEventBusContext.h"

// Optional adapter for hosts that want UiDispatcher wakeup messages to be
// delivered to a specific window instead of the dispatcher's internal hidden
// message window.

constexpr UINT kMfcUiDispatchDrainMessage = WM_APP + 1;

inline void ConfigureWindowWakeRouting(
    AppEventBusContext& context,
    HWND wakeWindow,
    UINT wakeMessage = kMfcUiDispatchDrainMessage) {
    context.configureMessageWakeup(wakeWindow, wakeMessage);
}

inline void ConfigureWindowWakeRouting(
    AppEventBusContext& context,
    const CWnd& wakeWindow,
    UINT wakeMessage = kMfcUiDispatchDrainMessage) {
    context.configureMessageWakeup(wakeWindow.GetSafeHwnd(), wakeMessage);
}

inline void ClearWindowWakeRouting(AppEventBusContext& context) {
    context.clearMessageWakeup();
}

inline LRESULT HandleWindowWakeRoutingMessage(
    AppEventBusContext& context,
    WPARAM,
    LPARAM) {
    context.getUiDispatcher().drain();
    return 0;
}