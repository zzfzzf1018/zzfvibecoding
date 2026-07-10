<#
.SYNOPSIS
  ETF query tool - one-click build/run (Windows / PowerShell)

.DESCRIPTION
  1) Resolve Python (default: CodeBuddy bundled Python; override with -Python or env ETF_PYTHON)
  2) Install runtime deps (akshare optional; falls back to csindex/em sources)
  3) Optional -Seed to populate ETF list (enables name search, needs network)
  4) Start uvicorn in the foreground and open the browser

.EXAMPLE
  .\scripts\run.ps1
  .\scripts\run.ps1 -Seed
  .\scripts\run.ps1 -Port 8080 -NoBrowser
  .\scripts\run.ps1 -Dev
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
$root = Split-Path -Parent $root                          # project root
$src  = Join-Path $root 'src'

# 1) Resolve Python
if ($Python) {
  $py = $Python
} elseif ($env:ETF_PYTHON) {
  $py = $env:ETF_PYTHON
} else {
  $wb = "$env:USERPROFILE\.workbuddy\binaries\python\versions\3.14.3\python.exe"
  if (Test-Path $wb) { $py = $wb }
  elseif (Get-Command python -ErrorAction SilentlyContinue) { $py = 'python' }
  else { Write-Error "Python not found. Use -Python or set env ETF_PYTHON."; exit 1 }
}
Write-Host "==> Using Python: $py"
& $py --version

# 2) Install runtime dependencies
Write-Host "==> Installing base runtime dependencies..."
& $py -m pip install -q -r (Join-Path $root 'requirements-runtime.txt')
if ($LASTEXITCODE -ne 0) { Write-Warning "Some base dependencies failed to install. Check network/Python." }

Write-Host "==> Installing akshare (main data source; falls back if it fails)..."
& $py -m pip install -q akshare
if ($LASTEXITCODE -ne 0) { Write-Warning "akshare install failed: will use csindex/em fallback sources only (needs network)." }

# 3) Optional: seed ETF list
if ($Seed) {
  Write-Host "==> Seeding ETF basic list (network fetch, may take tens of seconds)..."
  $seedFile = Join-Path $root "scripts\_seed_tmp.py"
  $lines = @(
    "import os, sys",
    "sys.path.insert(0, os.environ['SEED_SRC'])",
    "from app.core.di import Container",
    "from app.scheduler.jobs import refresh_etf_basic",
    "refresh_etf_basic(Container())",
    "print('SEED_OK')"
  )
  Set-Content -Path $seedFile -Value $lines -Encoding UTF8
  $env:SEED_SRC = $src
  & $py $seedFile
  Remove-Item $seedFile -ErrorAction SilentlyContinue
}

# 4) Start service in the foreground (logs show in THIS window; closing it stops the service)
#    Must run under src/ so the 'app' package is importable.
Write-Host "==> Starting service: http://localhost:$Port/  (Ctrl+C to stop; close this window to stop)"
$argList = @('-m', 'uvicorn', 'main:app', '--host', '0.0.0.0', '--port', "$Port")
if ($Dev) { $argList += '--reload' }

# Open browser after a short delay (does not block uvicorn logs)
if (-not $NoBrowser) {
  $url = "http://localhost:$Port/"
  Start-Job -ScriptBlock { param($u) Start-Sleep -Seconds 3; Start-Process $u } -ArgumentList $url | Out-Null
}

Push-Location $src
try {
  & $py @argList
}
finally {
  Pop-Location
}
