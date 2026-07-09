@echo off
chcp 65001 >nul
cd /d "%~dp0"

echo.
echo ================================
echo   股票财务查询系统 - 启动中...
echo ================================
echo.

if not exist "node_modules" (
    echo [INFO] 首次启动，正在安装依赖...
    echo.
    npm install
    if errorlevel 1 (
        echo [ERROR] 依赖安装失败，请检查网络连接
        pause
        exit /b 1
    )
    echo.
    echo [SUCCESS] 依赖安装完成
    echo.
)

if not exist "src\renderer\node_modules" (
    echo [INFO] 正在安装前端依赖...
    echo.
    npm run install:renderer
    if errorlevel 1 (
        echo [ERROR] 前端依赖安装失败
        pause
        exit /b 1
    )
    echo.
    echo [SUCCESS] 前端依赖安装完成
    echo.
)

if not exist "public\dist" (
    echo [INFO] 正在构建前端...
    echo.
    cd src\renderer
    npm run build
    cd ..\..
    if errorlevel 1 (
        echo [ERROR] 前端构建失败
        pause
        exit /b 1
    )
    echo.
    echo [SUCCESS] 前端构建完成
    echo.
)

echo [INFO] 正在启动应用...
echo.
echo ================================
echo   启动成功！
echo   请保持此窗口打开，关闭将退出应用
echo ================================
echo.

npm start

pause