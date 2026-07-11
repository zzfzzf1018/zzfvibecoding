@echo off
setlocal enabledelayedexpansion
chcp 65001 >nul
title 中国股市 ETF 查询工具

echo ============================================
echo   中国股市 ETF 查询工具 - 一键启动
echo ============================================
echo.

:: ============================================================
:: 按优先级收集候选 Python 路径（跳过 Windows Store 占位符）
:: ============================================================
set CANDIDATE_COUNT=0

:: 候选1: workbuddy 安装的 Python
if exist "%USERPROFILE%\.workbuddy\binaries\python\versions\3.11.9\python.exe" (
    set /a CANDIDATE_COUNT+=1
    set "CANDIDATE_!CANDIDATE_COUNT!=%USERPROFILE%\.workbuddy\binaries\python\versions\3.11.9\python.exe"
)

:: 候选2-6: 常见安装目录
for %%d in (
    "C:\Users\zzf\.workbuddy\binaries\python\versions\3.11.9\python.exe"
    "%LOCALAPPDATA%\Programs\Python\Python312\python.exe"
    "%LOCALAPPDATA%\Programs\Python\Python311\python.exe"
    "%LOCALAPPDATA%\Programs\Python\Python310\python.exe"
    "C:\Python312\python.exe"
    "C:\Python311\python.exe"
    "C:\Python310\python.exe"
) do (
    if exist %%d (
        set "already=0"
        for /l %%n in (1,1,!CANDIDATE_COUNT!) do (
            if "%%~d"=="!CANDIDATE_%%n!" set "already=1"
        )
        if "!already!"=="0" (
            set /a CANDIDATE_COUNT+=1
            set "CANDIDATE_!CANDIDATE_COUNT!=%%~d"
        )
    )
)

:: 候选: PATH 中的 python（排除 WindowsApps）
for /f "delims=" %%i in ('where python 2^>nul') do (
    echo %%i | findstr /i "WindowsApps" >nul
    if !errorlevel! neq 0 (
        set "already=0"
        for /l %%n in (1,1,!CANDIDATE_COUNT!) do (
            if "%%i"=="!CANDIDATE_%%n!" set "already=1"
        )
        if "!already!"=="0" (
            set /a CANDIDATE_COUNT+=1
            set "CANDIDATE_!CANDIDATE_COUNT!=%%i"
        )
    )
)

:: ============================================================
:: 逐个验证候选 Python
:: ============================================================
set PYTHON_EXE=
set TEST_INDEX=1

:test_next
if !TEST_INDEX! gtr !CANDIDATE_COUNT! goto :no_python_found

set "TRY_PATH=!CANDIDATE_%TEST_INDEX%!"
echo [信息] 尝试 Python: !TRY_PATH!
"!TRY_PATH!" --version >nul 2>&1
if !errorlevel! neq 0 (
    echo [警告] 该路径无法执行，跳过
    set /a TEST_INDEX+=1
    goto :test_next
)

"!TRY_PATH!" --version
set PYTHON_EXE=!TRY_PATH!
echo.

goto :start_venv

:no_python_found
echo.
echo [错误] 未检测到可用的 Python，请先安装 Python 3.10+
echo   下载地址: https://www.python.org/downloads/
echo   安装时请勾选 "Add Python to PATH"
pause
exit /b 1

:: ============================================================
:: 创建虚拟环境
:: ============================================================
:start_venv
cd /d "%~dp0"

if not exist ".venv" (
    echo [信息] 正在创建虚拟环境...
    "%PYTHON_EXE%" -m venv .venv
    if !errorlevel! neq 0 (
        echo [错误] 虚拟环境创建失败，请检查磁盘空间和写入权限
        pause
        exit /b 1
    )
    echo [成功] 虚拟环境创建完成
) else (
    echo [信息] 虚拟环境已存在，跳过创建
)
echo.

set "VENV_PYTHON=%CD%\.venv\Scripts\python.exe"
set "VENV_STREAMLIT=%CD%\.venv\Scripts\streamlit.exe"

:: ============================================================
:: 安装依赖
:: ============================================================
echo [信息] 正在安装/更新依赖包（首次运行可能需要几分钟）...
"%VENV_PYTHON%" -m pip install -r requirements.txt -q
if !errorlevel! neq 0 (
    echo [警告] 快速安装失败，切换详细安装模式...
    "%VENV_PYTHON%" -m pip install -r requirements.txt
    if !errorlevel! neq 0 (
        echo.
        echo [错误] 依赖安装失败，请检查网络连接
        echo   可尝试切换国内镜像:
        echo   %VENV_PYTHON% -m pip install -r requirements.txt -i https://pypi.tuna.tsinghua.edu.cn/simple
        pause
        exit /b 1
    )
)
echo [成功] 依赖安装完成
echo.

:: ============================================================
:: 启动应用
:: ============================================================
if not exist "logs" mkdir logs

echo [信息] 正在启动应用...
echo [信息] 浏览器将自动打开（或手动访问: http://localhost:8501）
echo [信息] 按 Ctrl+C 可停止服务
echo.

"%VENV_STREAMLIT%" run app.py --server.port=8501 --server.address=0.0.0.0

pause
