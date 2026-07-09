@echo off
title ETF Tool Launcher

echo ==============================================
echo          ETF Tool - One-click Launcher
echo ==============================================
echo.
echo Project: EtfTool
echo Docs: docs/
echo - requirements.md   : Requirements
echo - design.md         : Design
echo - ai-guidance.md    : AI Guidance
echo ==============================================
echo.

if not exist "EtfTool.sln" (
    echo [ERROR] Solution file not found: EtfTool.sln
    echo [TIP] Run this script from project root directory
    pause
    exit /b 1
)

echo [1/5] Checking .NET SDK installation...
dotnet --version >nul 2>&1
if %errorlevel% neq 0 (
    echo [ERROR] .NET SDK not installed
    echo [TIP] Download from https://dotnet.microsoft.com/download
    pause
    exit /b 1
)

for /f "tokens=*" %%i in ('dotnet --version') do set DOTNET_VERSION=%%i
echo [OK] .NET SDK installed (Version: %DOTNET_VERSION%)
echo.

echo [2/5] Restoring dependencies...
dotnet restore EtfTool.sln --source https://api.nuget.org/v3/index.json
if %errorlevel% neq 0 (
    echo [ERROR] Failed to restore dependencies
    echo [TIP] Check network connection
    pause
    exit /b 1
)

echo [OK] Dependencies restored successfully
echo.

echo [3/5] Building project...
dotnet build EtfTool.sln --configuration Release --source https://api.nuget.org/v3/index.json
if %errorlevel% neq 0 (
    echo [ERROR] Build failed
    echo [TIP] Check code errors
    pause
    exit /b 1
)

echo [OK] Build succeeded
echo.

echo [4/5] Starting application...
echo ==============================================
echo.

dotnet run --project EtfTool.Wpf\EtfTool.Wpf.csproj --configuration Release

echo.
echo ==============================================
echo [5/5] Application exited
echo [TIP] Run this script again to restart
pause