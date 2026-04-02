#pragma once

#include <Windows.h>

#include <atomic>
#include <string>
#include <thread>

#include "UiTaskDispatcher.h"

class MainWindow
{
public:
    MainWindow();
    ~MainWindow();

    bool Create(HINSTANCE instance);
    HWND GetHwnd() const;

    static const UINT kUiDispatchMessage;

private:
    static LRESULT CALLBACK WindowProc(HWND hwnd, UINT message, WPARAM wParam, LPARAM lParam);
    LRESULT HandleMessage(UINT message, WPARAM wParam, LPARAM lParam);

    bool RegisterWindowClass(HINSTANCE instance);
    void CreateChildControls();
    void StartWorker();
    void StopWorker();
    void SetStatusText(const std::wstring& text);
    void AppendLogText(const std::wstring& text);

    HWND hwnd_;
    HWND statusLabel_;
    HWND startButton_;
    HWND logEdit_;
    UiTaskDispatcher dispatcher_;
    std::thread workerThread_;
    std::atomic<bool> workerRunning_;
    std::atomic<bool> stopRequested_;
};