@echo off
REM ============================================================
REM  ETF 查询工具 —— Windows 一键启动（双击本文件即可）
REM  - 自动绕过 PowerShell 执行策略限制（避免"双击没反应"）
REM  - 在同一窗口显示启动日志，窗口不会神秘消失
REM  - 如需自定义参数，请用 PowerShell 直接运行 scripts\run.ps1
REM    （例如： powershell -ExecutionPolicy Bypass -File scripts\run.ps1 -Seed）
REM ============================================================
cd /d "%~dp0"
powershell -ExecutionPolicy Bypass -NoExit -File "%~dp0scripts\run.ps1"
