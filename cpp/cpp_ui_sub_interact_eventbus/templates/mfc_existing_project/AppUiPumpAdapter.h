#pragma once

#include <afxwin.h>

#include <eventbus/UiPumpHelper.h>

#include "AppEventBusContext.h"

template <typename TMfcApp>
int RunMfcUiLoop(
    TMfcApp& app,
    AppEventBusContext& context,
    const UiPumpHelper::WaitErrorHandler& onWaitFailed = UiPumpHelper::WaitErrorHandler()) {
    return UiPumpHelper::runLoop(
        context.getUiDispatcher(),
        [&app]() {
            return app.PumpMessage() != FALSE;
        },
        [&app]() {
            return app.ExitInstance();
        },
        onWaitFailed);
}