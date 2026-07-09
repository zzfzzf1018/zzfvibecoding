@echo off
echo ============================================
echo   中国股市ETF查询工具 - 一键启动脚本
echo ============================================
echo.

set NODE_PATH=C:\Program Files\nodejs
set PATH=%NODE_PATH%;%PATH%

echo 检查Node.js环境...
node -v >nul 2>&1
if %errorlevel% neq 0 (
    echo 错误: 未找到Node.js，请先安装Node.js >= 18.0.0
    echo 下载地址: https://nodejs.org/
    pause
    exit /b 1
)

echo Node.js版本:
node -v

echo.
echo 检查npm环境...
npm -v >nul 2>&1
if %errorlevel% neq 0 (
    echo 错误: 未找到npm
    pause
    exit /b 1
)

echo npm版本:
npm -v

echo.
echo 安装项目依赖...
npm install

if %errorlevel% neq 0 (
    echo 错误: 依赖安装失败
    pause
    exit /b 1
)

echo.
echo 依赖安装成功!
echo.

echo 正在编译项目...
npm run build

if %errorlevel% neq 0 (
    echo 警告: 生产编译失败，将使用开发模式启动
) else (
    echo 编译成功!
)

echo.
echo 启动开发服务器...
echo 访问地址: http://localhost:5173
echo.
echo 开发模式会自动检测文件变化并热更新，无需手动重启
echo.

npm run dev

pause