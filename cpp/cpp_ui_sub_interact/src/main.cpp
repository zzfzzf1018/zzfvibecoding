#include <Windows.h>

#include "MainWindow.h"

int APIENTRY wWinMain(HINSTANCE instance, HINSTANCE, LPWSTR, int commandShow)
{
    MainWindow mainWindow;
    if (!mainWindow.Create(instance))
    {
        return -1;
    }

    ::ShowWindow(mainWindow.GetHwnd(), commandShow);
    ::UpdateWindow(mainWindow.GetHwnd());

    MSG message = {};
    while (::GetMessage(&message, NULL, 0, 0) > 0)
    {
        ::TranslateMessage(&message);
        ::DispatchMessage(&message);
    }

    return static_cast<int>(message.wParam);
}