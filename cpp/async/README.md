# MFC Async/Await Demo

这是一个最小 MFC 示例工程，演示类似 C# `async/await` 的编程方式：

- 点击按钮后，业务逻辑在后台线程执行。
- 任务完成后，自动切回 UI 线程更新界面。
- UI 线程不会被阻塞，不会卡界面。
- 支持任务取消（类似 `CancellationToken`）。
- 支持后台任务进度回调到 UI 线程（进度条实时更新）。
- 新增链式任务示例：任务 A 完成后在续体中启动任务 B。

## 结构

- `src/AsyncAwait.h`：异步封装（`Await` + UI 续体队列）
- `src/MainFrame.h` / `src/MainFrame.cpp`：窗口和按钮点击示例
- `src/main.cpp`：MFC 应用入口
- `CMakeLists.txt`：使用 CMake 构建 MFC 项目

## 构建方式（Visual Studio 2022）

1. 安装 Visual Studio 的 **MFC/ATL** 组件。
2. 在项目根目录执行：

```powershell
cmake -S . -B build -G "Visual Studio 17 2022" -A x64
cmake --build build --config Release
```

3. 运行：

```powershell
.\build\Release\MfcAsyncAwaitDemo.exe
```

## 核心思路

`Await` 封装做了两件事：

1. 把耗时业务逻辑放在 `std::thread` 中执行。
2. 任务完成后把 continuation 投递到 UI 消息队列（`PostMessage`），在 UI 线程里执行。

这和 C# 的 `await` 概念一致：

- `work` 对应后台异步任务。
- `continuation` 对应 `await` 后恢复执行的代码（此处恢复到 UI 线程）。

新增 `AwaitCancellableProgress`：

- `CancellationTokenSource`：用于 UI 发起取消请求。
- `CancellationToken`：在后台线程检查是否需要取消。
- `reportProgress(int)`：后台线程上报进度，最终在 UI 线程更新控件。

## 链式 await 示例

界面里新增了“执行链式任务(A->B)”按钮：

- 阶段 A 在后台执行，进度映射到 1%~50%。
- A 的 continuation 在 UI 线程执行，并发起阶段 B。
- 阶段 B 在后台执行，进度映射到 51%~100%。
- 任意阶段都可通过“取消任务”按钮中断。
