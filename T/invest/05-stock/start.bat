@echo off
chcp 65001 >nul
title 股票分析工具

setlocal enabledelayedexpansion

set "PYTHON_PATH="
set "VENV_DIR=venv"
set "REQUIREMENTS=requirements.txt"
set "MAIN_SCRIPT=gui.py"

echo ============================================
echo        股票分析工具 - 一键启动脚本
echo ============================================

call :find_python

if not defined PYTHON_PATH (
    echo.
    echo [错误] 未找到有效的Python环境！
    echo.
    echo 检测到的python.exe可能是Microsoft Store的占位程序，
    echo 无法创建虚拟环境。请按照以下步骤安装Python：
    echo.
    echo 1. 访问 https://www.python.org/downloads/
    echo 2. 下载Python 3.9或更高版本
    echo 3. 安装时务必勾选 "Add Python to PATH"
    echo 4. 重新运行此脚本
    echo.
    pause
    exit /b 1
)

echo [OK] 找到Python: !PYTHON_PATH!
echo.

call :check_venv

echo ============================================
echo          启动股票分析工具
echo ============================================
echo.

call "%VENV_DIR%\Scripts\python.exe" "%MAIN_SCRIPT%" %*

echo.
echo ============================================
echo               程序已退出
echo ============================================
pause
exit /b 0

:find_python
echo [查找] 正在搜索Python环境...

call :try_py_launcher
if defined PYTHON_PATH goto :found_python

call :check_installation_paths
if defined PYTHON_PATH goto :found_python

call :check_conda_paths
if defined PYTHON_PATH goto :found_python

call :check_where_with_filter
if defined PYTHON_PATH goto :found_python

:found_python
exit /b

:try_py_launcher
echo [查找] 尝试py启动器...
for /f "tokens=*" %%a in ('py -3 --version 2^>nul') do (
    set "PY_VER=%%a"
    for /f "tokens=*" %%p in ('py -3 -c "import sys; print(sys.executable)" 2^>nul') do (
        set "PYTHON_PATH=%%p"
        echo [查找] py启动器找到: !PY_VER!
        goto :eof
    )
)
exit /b

:check_installation_paths
echo [查找] 检查常见安装路径...

for /d %%d in ("%LOCALAPPDATA%\Programs\Python\Python3*") do (
    if exist "%%d\python.exe" (
        set "PYTHON_PATH=%%d\python.exe"
        echo [查找] 在 %%d 找到
        goto :eof
    )
)

for /d %%d in ("C:\Python3*") do (
    if exist "%%d\python.exe" (
        set "PYTHON_PATH=%%d\python.exe"
        echo [查找] 在 %%d 找到
        goto :eof
    )
)

for /d %%d in ("C:\Program Files\Python3*") do (
    if exist "%%d\python.exe" (
        set "PYTHON_PATH=%%d\python.exe"
        echo [查找] 在 %%d 找到
        goto :eof
    )
)

for /d %%d in ("C:\Program Files (x86)\Python3*") do (
    if exist "%%d\python.exe" (
        set "PYTHON_PATH=%%d\python.exe"
        echo [查找] 在 %%d 找到
        goto :eof
    )
)
exit /b

:check_conda_paths
echo [查找] 检查conda环境...

if exist "%USERPROFILE%\miniconda3\python.exe" (
    set "PYTHON_PATH=%USERPROFILE%\miniconda3\python.exe"
    echo [查找] 在 miniconda3 找到
    goto :eof
)

if exist "%USERPROFILE%\anaconda3\python.exe" (
    set "PYTHON_PATH=%USERPROFILE%\anaconda3\python.exe"
    echo [查找] 在 anaconda3 找到
    goto :eof
)

if exist "%LOCALAPPDATA%\miniconda3\python.exe" (
    set "PYTHON_PATH=%LOCALAPPDATA%\miniconda3\python.exe"
    echo [查找] 在 local miniconda3 找到
    goto :eof
)
exit /b

:check_where_with_filter
echo [查找] 通过PATH查找（过滤WindowsApps）...

for %%p in (python python3) do (
    for /f "tokens=*" %%a in ('where %%p 2^>nul') do (
        set "CANDIDATE=%%a"
        echo !CANDIDATE! | findstr /i "WindowsApps" >nul
        if !errorlevel! neq 0 (
            if exist "%%a" (
                set "PYTHON_PATH=%%a"
                echo [查找] 在PATH中找到: !PYTHON_PATH!
                goto :eof
            )
        )
    )
)
exit /b

:check_venv
if exist "%VENV_DIR%" (
    if not exist "%VENV_DIR%\Scripts\python.exe" (
        echo [修复] 检测到损坏的虚拟环境，正在删除重建...
        rd /s /q "%VENV_DIR%"
    )
)

if not exist "%VENV_DIR%" (
    echo [创建] 正在创建虚拟环境...
    "%PYTHON_PATH%" -m venv "%VENV_DIR%"
    if !errorlevel! neq 0 (
        echo [错误] 创建虚拟环境失败！
        echo.
        echo 可能原因：
        echo 1. 当前Python版本不支持venv模块
        echo 2. 权限不足，无法创建目录
        echo.
        pause
        exit /b 1
    )
)

if not exist "%VENV_DIR%\Scripts\pip.exe" (
    echo [更新] 正在更新pip...
    "%VENV_DIR%\Scripts\python.exe" -m ensurepip
)

echo [检查] 正在安装依赖...
"%VENV_DIR%\Scripts\pip.exe" install -r "%REQUIREMENTS%"
if !errorlevel! neq 0 (
    echo [警告] 部分依赖安装失败，程序可能无法正常运行
    echo.
)

exit /b