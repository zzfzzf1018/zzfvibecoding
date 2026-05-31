<#
.SYNOPSIS
  One-shot build script for CloudNote (Windows + Android).

.DESCRIPTION
  Verifies Flutter, fetches dependencies, runs tests, then builds both
  the Windows desktop bundle and the Android APK.

.PARAMETER ClientId
  GitHub OAuth Device Flow client id. Defaults to env:GITHUB_CLIENT_ID.

.PARAMETER SkipTests
  Skip `flutter test`.

.PARAMETER Targets
  Comma-separated list. Supported: windows, android. Default: both.

.EXAMPLE
  ./scripts/build_all.ps1 -ClientId Iv1.abc123
#>
[CmdletBinding()]
param(
  [string]$ClientId = $env:GITHUB_CLIENT_ID,
  [switch]$SkipTests,
  [string]$Targets = 'windows,android'
)

$ErrorActionPreference = 'Stop'
$root = Resolve-Path (Join-Path $PSScriptRoot '..')
Set-Location $root

function Require-Cmd($name) {
  if (-not (Get-Command $name -ErrorAction SilentlyContinue)) {
    throw "Required command '$name' not found in PATH."
  }
}

Require-Cmd flutter
if (-not $ClientId) {
  Write-Warning "GITHUB_CLIENT_ID not set; building with placeholder. Auth will not work at runtime."
}

Write-Host '==> flutter pub get' -ForegroundColor Cyan
flutter pub get

if (-not (Test-Path 'android') -or -not (Test-Path 'windows')) {
  Write-Host '==> Generating platform folders (one-time)' -ForegroundColor Cyan
  flutter create --platforms=windows,android --project-name cloudnote .
}

if (-not $SkipTests) {
  Write-Host '==> flutter test' -ForegroundColor Cyan
  flutter test
}

$defines = @()
if ($ClientId) { $defines += "--dart-define=GITHUB_CLIENT_ID=$ClientId" }

$targetList = $Targets.Split(',') | ForEach-Object { $_.Trim().ToLower() }

if ($targetList -contains 'windows') {
  Write-Host '==> Build: Windows' -ForegroundColor Cyan
  flutter build windows --release @defines
}

if ($targetList -contains 'android') {
  Write-Host '==> Build: Android (APK)' -ForegroundColor Cyan
  flutter build apk --release @defines
}

Write-Host 'Done.' -ForegroundColor Green
