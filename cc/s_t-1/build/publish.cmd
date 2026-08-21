@echo off
REM 便捷入口：发布 win-x64 桌面程序到 artifacts 目录
setlocal
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0publish.ps1" %*
endlocal
