<#
.SYNOPSIS
  ETF 查询工具 —— 一键编译/运行（Windows / PowerShell）

.DESCRIPTION
  1) 解析 Python（默认用 CodeBuddy 内置 Python；可用 -Python 覆盖，或设置环境变量 ETF_PYTHON）
  2) 安装运行时依赖（akshare 安装失败则降级到中证/东财兜底源，仍可运行）
  3) 可选 -Seed 播种 ETF 列表（让名称搜索可用，需联网）
  4) 启动 uvicorn 并打开浏览器

.EXAMPLE
  .\scripts\run.ps1                 # 安装依赖 + 启动 + 打开浏览器
  .\scripts\run.ps1 -Seed           # 启动前先播种 ETF 列表
  .\scripts\run.ps1 -Port 8080 -NoBrowser
  .\scripts\run.ps1 -Dev            # 热重载
#>
[CmdletBinding()]
param(
  [int]$Port = 8000,
  [switch]$NoBrowser,
  [switch]$Dev,
  [switch]$Seed,
  [string]$Python
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Path   # scripts/
$root = Split-Path -Parent $root                          # 项目根
$src  = Join-Path $root 'src'

# 1) 解析 Python
if ($Python) {
  $py = $Python
} elseif ($env:ETF_PYTHON) {
  $py = $env:ETF_PYTHON
} else {
  $wb = "$env:USERPROFILE\.workbuddy\binaries\python\versions\3.14.3\python.exe"
  if (Test-Path $wb) { $py = $wb }
  elseif (Get-Command python -ErrorAction SilentlyContinue) { $py = 'python' }
  else { Write-Error "未找到 Python。请用 -Python 指定，或设置环境变量 ETF_PYTHON。"; exit 1 }
}
Write-Host "==> 使用 Python: $py"
& $py --version

# 2) 安装运行时依赖
Write-Host "==> 安装运行时基础依赖..."
& $py -m pip install -q -r (Join-Path $root 'requirements-runtime.txt')
if ($LASTEXITCODE -ne 0) { Write-Warning "部分基础依赖安装失败，请检查网络/Python 环境。" }

Write-Host "==> 安装主数据源 akshare（失败则降级到中证/东财兜底源）..."
& $py -m pip install -q akshare
if ($LASTEXITCODE -ne 0) { Write-Warning "akshare 安装失败：将仅使用中证/东财兜底源（需联网）。" }

# 3) 可选：播种 ETF 列表
if ($Seed) {
  Write-Host "==> 播种 ETF 基础列表（联网拉取，可能需要几十秒）..."
  $seedCode = @"
import sys; sys.path.insert(0, r'$src')
from app.core.di import Container
from app.scheduler.jobs import refresh_etf_basic
refresh_etf_basic(Container())
print('SEED_OK')
"@
  & $py -c $seedCode
}

# 4) 启动服务（需在 src 目录运行，app 包才可导入）
Write-Host "==> 启动服务: http://localhost:$Port/  (Ctrl+C 退出)"
$argList = @('-m', 'uvicorn', 'main:app', '--host', '0.0.0.0', '--port', "$Port")
if ($Dev) { $argList += '--reload' }

$proc = Start-Process -FilePath $py -ArgumentList $argList -WorkingDirectory $src -PassThru
if (-not $NoBrowser) {
  Start-Sleep -Seconds 3
  Start-Process "http://localhost:$Port/"
}

try {
  $proc.WaitForExit()
} finally {
  if (-not $proc.HasExited) { $proc.Kill() }
}
