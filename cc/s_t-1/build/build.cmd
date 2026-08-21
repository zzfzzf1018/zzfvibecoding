@echo off
REM 便捷入口：双击即可完成 还原 -> 编译 -> 测试
setlocal
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0build.ps1" %*
endlocal
