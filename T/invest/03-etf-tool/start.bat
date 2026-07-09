
@echo off
chcp 65001 >nul
title 大A ETF工具 - 启动器

echo ==============================================
echo           大A ETF工具 - 一键启动脚本
echo ==============================================
echo.
echo 项目地址: https://github.com/example/etf-tool
echo 文档目录: docs/
echo - requirements.md   : 需求文档
echo - design.md         : 设计文档
echo - ai-guidance.md    : AI开发指导文档
echo ==============================================
echo.

if not exist "EtfTool.sln" (
    echo [错误] 未找到解决方案文件 EtfTool.sln
    echo [提示] 请确保在项目根目录下运行此脚本
    pause
    exit /b 1
)

echo [1/5] 正在检查 .NET SDK 是否安装...
dotnet --version >nul 2>&1
if %errorlevel% neq 0 (
    echo [错误] 未安装 .NET SDK
    echo [提示] 请访问 https://dotnet.microsoft.com/download 安装 .NET 6.0 或更高版本
    pause
    exit /b 1
)

for /f "tokens=*" %%i in ('dotnet --version') do set DOTNET_VERSION=%%i
echo [完成] .NET SDK 已安装 (版本: %DOTNET_VERSION%)
echo.

echo [2/5] 正在还原依赖包...
dotnet restore EtfTool.sln --source https://api.nuget.org/v3/index.json
if %errorlevel% neq 0 (
    echo [错误] 依赖包还原失败
    echo [提示] 请检查网络连接或代理设置
    pause
    exit /b 1
)

echo [完成] 依赖包还原成功
echo.

echo [3/5] 正在构建项目...
dotnet build EtfTool.sln --configuration Release --source https://api.nuget.org/v3/index.json
if %errorlevel% neq 0 (
    echo [错误] 项目构建失败
    echo [提示] 请检查代码错误或项目配置
    pause
    exit /b 1
)

echo [完成] 项目构建成功
echo.

echo [4/5] 正在启动应用...
echo ==============================================
echo.

dotnet run --project EtfTool.Wpf\EtfTool.Wpf.csproj --configuration Release

echo.
echo ==============================================
echo [5/5] 应用已退出
echo [提示] 如需重新启动，请再次运行此脚本
pause
