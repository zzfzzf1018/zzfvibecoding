<#
.SYNOPSIS
    还原、编译并测试 StockAnalyzer 解决方案。

.DESCRIPTION
    默认执行「restore -> build -> test」全流程。
    脚本会自动定位仓库根目录，可在任意工作目录下调用。

.PARAMETER Configuration
    编译配置，Debug 或 Release，默认 Release。

.PARAMETER SkipTests
    跳过单元测试。

.PARAMETER NoRestore
    跳过 NuGet 还原（用于已还原过的增量编译）。

.EXAMPLE
    ./build/build.ps1

.EXAMPLE
    ./build/build.ps1 -Configuration Debug -SkipTests
#>
[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',

    [switch]$SkipTests,

    [switch]$NoRestore
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repoRoot = Split-Path -Parent $PSScriptRoot
$solution = Join-Path $repoRoot 'StockAnalyzer.sln'

function Invoke-Step {
    param([string]$Name, [scriptblock]$Action)

    Write-Host ''
    Write-Host "==> $Name" -ForegroundColor Cyan
    & $Action

    if ($LASTEXITCODE -ne 0) {
        throw "步骤失败：$Name（退出码 $LASTEXITCODE）"
    }
}

Write-Host "仓库根目录 : $repoRoot"
Write-Host "解决方案   : $solution"
Write-Host "编译配置   : $Configuration"

Invoke-Step '检查 .NET SDK' { dotnet --version }

if (-not $NoRestore) {
    Invoke-Step '还原 NuGet 包' { dotnet restore $solution }
}

# 上一步已完成还原，编译阶段无需重复
$buildArgs = @($solution, '-c', $Configuration, '--nologo', '--no-restore')

Invoke-Step '编译' { dotnet build @buildArgs }

if (-not $SkipTests) {
    Invoke-Step '单元测试' {
        dotnet test $solution -c $Configuration --no-build --nologo --verbosity minimal
    }
}

Write-Host ''
Write-Host '构建完成。' -ForegroundColor Green
Write-Host ("桌面程序输出：" + (Join-Path $repoRoot "src\StockAnalyzer.Desktop\bin\$Configuration\net8.0-windows\StockAnalyzer.Desktop.exe"))
