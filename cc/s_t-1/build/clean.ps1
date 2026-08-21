<#
.SYNOPSIS
    清理编译产物。

.PARAMETER IncludeLocalData
    同时删除本地数据库（%LOCALAPPDATA%\StockAnalyzer）。该操作会清空自选股，请谨慎使用。
#>
[CmdletBinding(SupportsShouldProcess)]
param(
    [switch]$IncludeLocalData
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repoRoot = Split-Path -Parent $PSScriptRoot

Write-Host "清理 bin / obj / artifacts …"

Get-ChildItem -Path $repoRoot -Include 'bin', 'obj' -Recurse -Directory -ErrorAction SilentlyContinue |
    Where-Object { $_.FullName -notlike '*\.git\*' } |
    ForEach-Object {
        if ($PSCmdlet.ShouldProcess($_.FullName, '删除')) {
            Remove-Item $_.FullName -Recurse -Force -ErrorAction SilentlyContinue
        }
    }

$artifacts = Join-Path $repoRoot 'artifacts'
if (Test-Path $artifacts) {
    if ($PSCmdlet.ShouldProcess($artifacts, '删除')) {
        Remove-Item $artifacts -Recurse -Force
    }
}

if ($IncludeLocalData) {
    $dataDir = Join-Path $env:LOCALAPPDATA 'StockAnalyzer'
    if (Test-Path $dataDir) {
        Write-Warning "即将删除本地数据库目录：$dataDir（自选股将丢失）"
        if ($PSCmdlet.ShouldProcess($dataDir, '删除')) {
            Remove-Item $dataDir -Recurse -Force
        }
    }
}

Write-Host '清理完成。' -ForegroundColor Green
