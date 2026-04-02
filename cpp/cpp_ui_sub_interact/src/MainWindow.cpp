#include "MainWindow.h"

#include <WindowsX.h>

#include <chrono>
#include <sstream>
#include <utility>

namespace
{
    const wchar_t kWindowClassName[] = L"CppUiSubInteractWindow";
    const int kStartButtonId = 1001;
}

const UINT MainWindow::kUiDispatchMessage = WM_APP + 1;

MainWindow::MainWindow()
    : hwnd_(NULL),
      statusLabel_(NULL),
      startButton_(NULL),
      logEdit_(NULL),
      workerRunning_(false),
      stopRequested_(false)
{
}

MainWindow::~MainWindow()
{
    StopWorker();
}

bool MainWindow::Create(HINSTANCE instance)
{
    if (!RegisterWindowClass(instance))
    {
        return false;
    }

    hwnd_ = ::CreateWindowEx(
        0,
        kWindowClassName,
        L"子线程通知主 UI 线程示例",
        WS_OVERLAPPEDWINDOW | WS_VISIBLE,
        CW_USEDEFAULT,
        CW_USEDEFAULT,
        700,
        420,
        NULL,
        NULL,
        instance,
        this);

    return hwnd_ != NULL;
}

HWND MainWindow::GetHwnd() const
{
    return hwnd_;
}

LRESULT CALLBACK MainWindow::WindowProc(HWND hwnd, UINT message, WPARAM wParam, LPARAM lParam)
{
    MainWindow* window = reinterpret_cast<MainWindow*>(::GetWindowLongPtr(hwnd, GWLP_USERDATA));

    if (message == WM_NCCREATE)
    {
        CREATESTRUCT* createStruct = reinterpret_cast<CREATESTRUCT*>(lParam);
        window = reinterpret_cast<MainWindow*>(createStruct->lpCreateParams);
        if (window != NULL)
        {
            window->hwnd_ = hwnd;
            ::SetWindowLongPtr(hwnd, GWLP_USERDATA, reinterpret_cast<LONG_PTR>(window));
        }
    }

    if (window != NULL)
    {
        return window->HandleMessage(message, wParam, lParam);
    }

    return ::DefWindowProc(hwnd, message, wParam, lParam);
}

LRESULT MainWindow::HandleMessage(UINT message, WPARAM wParam, LPARAM lParam)
{
    switch (message)
    {
    case WM_CREATE:
        dispatcher_.Initialize(hwnd_, kUiDispatchMessage);
        CreateChildControls();
        SetStatusText(L"等待任务启动");
        AppendLogText(L"主窗口初始化完成。\r\n");
        return 0;

    case WM_COMMAND:
        if (LOWORD(wParam) == kStartButtonId && HIWORD(wParam) == BN_CLICKED)
        {
            StartWorker();
            return 0;
        }
        break;

    case kUiDispatchMessage:
        dispatcher_.DispatchPendingTasks();
        return 0;

    case WM_CLOSE:
        StopWorker();
        dispatcher_.Shutdown();
        ::DestroyWindow(hwnd_);
        return 0;

    case WM_DESTROY:
        ::PostQuitMessage(0);
        return 0;
    }

    return ::DefWindowProc(hwnd_, message, wParam, lParam);
}

bool MainWindow::RegisterWindowClass(HINSTANCE instance)
{
    WNDCLASSEX windowClass = {};
    windowClass.cbSize = sizeof(windowClass);
    windowClass.lpfnWndProc = &MainWindow::WindowProc;
    windowClass.hInstance = instance;
    windowClass.lpszClassName = kWindowClassName;
    windowClass.hCursor = ::LoadCursor(NULL, IDC_ARROW);
    windowClass.hbrBackground = reinterpret_cast<HBRUSH>(COLOR_WINDOW + 1);
    windowClass.style = CS_HREDRAW | CS_VREDRAW;

    return ::RegisterClassEx(&windowClass) != 0 || ::GetLastError() == ERROR_CLASS_ALREADY_EXISTS;
}

void MainWindow::CreateChildControls()
{
    statusLabel_ = ::CreateWindowEx(
        0,
        L"STATIC",
        L"",
        WS_CHILD | WS_VISIBLE,
        20,
        20,
        620,
        24,
        hwnd_,
        NULL,
        ::GetModuleHandle(NULL),
        NULL);

    startButton_ = ::CreateWindowEx(
        0,
        L"BUTTON",
        L"启动子线程任务",
        WS_TABSTOP | WS_CHILD | WS_VISIBLE | BS_DEFPUSHBUTTON,
        20,
        60,
        160,
        32,
        hwnd_,
        reinterpret_cast<HMENU>(static_cast<INT_PTR>(kStartButtonId)),
        ::GetModuleHandle(NULL),
        NULL);

    logEdit_ = ::CreateWindowEx(
        WS_EX_CLIENTEDGE,
        L"EDIT",
        L"",
        WS_CHILD | WS_VISIBLE | WS_VSCROLL | ES_LEFT | ES_MULTILINE | ES_AUTOVSCROLL | ES_READONLY,
        20,
        110,
        640,
        230,
        hwnd_,
        NULL,
        ::GetModuleHandle(NULL),
        NULL);
}

void MainWindow::StartWorker()
{
    if (workerRunning_.exchange(true))
    {
        AppendLogText(L"已有业务线程正在运行，请等待完成。\r\n");
        return;
    }

    stopRequested_.store(false);
    ::EnableWindow(startButton_, FALSE);
    SetStatusText(L"子线程任务启动中...");
    AppendLogText(L"准备启动业务线程。\r\n");

    if (workerThread_.joinable())
    {
        workerThread_.join();
    }

    workerThread_ = std::thread([this]() {
        dispatcher_.PostTask([this]() {
            AppendLogText(L"业务线程已启动。\r\n");
        });

        for (int step = 1; step <= 5; ++step)
        {
            if (stopRequested_.load())
            {
                dispatcher_.PostTask([this]() {
                    SetStatusText(L"任务已取消");
                    AppendLogText(L"业务线程收到停止请求。\r\n");
                    ::EnableWindow(startButton_, TRUE);
                });

                workerRunning_.store(false);
                return;
            }

            std::this_thread::sleep_for(std::chrono::milliseconds(700));

            std::wstringstream stream;
            stream << L"业务步骤 " << step << L"/5 完成";
            const std::wstring statusText = stream.str();

            dispatcher_.PostTask([this, statusText]() {
                SetStatusText(statusText);
                AppendLogText(statusText + L"，由主线程更新 UI。\r\n");
            });
        }

        dispatcher_.PostTask([this]() {
            SetStatusText(L"全部业务处理完成");
            AppendLogText(L"业务线程结束，主线程已完成最终 UI 刷新。\r\n");
            ::EnableWindow(startButton_, TRUE);
        });

        workerRunning_.store(false);
    });
}

void MainWindow::StopWorker()
{
    stopRequested_.store(true);

    if (workerThread_.joinable())
    {
        workerThread_.join();
    }

    workerRunning_.store(false);
}

void MainWindow::SetStatusText(const std::wstring& text)
{
    if (statusLabel_ != NULL)
    {
        ::SetWindowText(statusLabel_, text.c_str());
    }
}

void MainWindow::AppendLogText(const std::wstring& text)
{
    if (logEdit_ == NULL)
    {
        return;
    }

    const int length = ::GetWindowTextLength(logEdit_);
    ::SendMessage(logEdit_, EM_SETSEL, static_cast<WPARAM>(length), static_cast<LPARAM>(length));
    ::SendMessage(logEdit_, EM_REPLACESEL, FALSE, reinterpret_cast<LPARAM>(text.c_str()));
}