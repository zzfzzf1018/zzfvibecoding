<#
.SYNOPSIS
    发布 StockAnalyzer 桌面程序。

.DESCRIPTION
    默认发布为「依赖框架」的 win-x64 版本，体积小但需要目标机器安装 .NET 8 桌面运行时。
    使用 -SelfContained 可发布为自包含版本，目标机器无需预装运行时。

.PARAMETER Configuration
    编译配置，默认 Release。

.PARAMETER Runtime
    运行时标识符，默认 win-x64。

.PARAMETER OutputPath
    输出目录，默认 <仓库根>/artifacts/<Runtime>。

.PARAMETER SelfContained
    发布自包含版本（内置 .NET 运行时）。

.PARAMETER SingleFile
    打包为单文件可执行程序（需配合 -SelfContained 才能真正独立运行）。

.EXAMPLE
    ./build/publish.ps1

.EXAMPLE
    ./build/publish.ps1 -SelfContained -SingleFile
#>
[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',

    [string]$Runtime = 'win-x64',

    [string]$OutputPath,

    [switch]$SelfContained,

    [switch]$SingleFile
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repoRoot = Split-Path -Parent $PSScriptRoot
$project = Join-Path $repoRoot 'src\StockAnalyzer.Desktop\StockAnalyzer.Desktop.csproj'

if (-not $OutputPath) {
    $OutputPath = Join-Path $repoRoot "artifacts\$Runtime"
}

if (Test-Path $OutputPath) {
    Write-Host "清理旧的发布目录：$OutputPath"
    Remove-Item $OutputPath -Recurse -Force
}

$arguments = @(
    $project,
    '-c', $Configuration,
    '-r', $Runtime,
    '-o', $OutputPath,
    '--nologo'
)

if ($SelfContained) {
    $arguments += '--self-contained'
    $arguments += 'true'
}
else {
    $arguments += '--self-contained'
    $arguments += 'false'
}

if ($SingleFile) {
    $arguments += '-p:PublishSingleFile=true'
    $arguments += '-p:IncludeNativeLibrariesForSelfExtract=true'
}

# WPF 不支持裁剪，这里显式关闭以免出现运行时反射失败
$arguments += '-p:PublishTrimmed=false'
$arguments += '-p:PublishReadyToRun=true'

Write-Host "发布项目   : $project"
Write-Host "运行时     : $Runtime"
Write-Host "自包含     : $SelfContained"
Write-Host "单文件     : $SingleFile"
Write-Host "输出目录   : $OutputPath"
Write-Host ''

dotnet publish @arguments

if ($LASTEXITCODE -ne 0) {
    throw "发布失败（退出码 $LASTEXITCODE）"
}

Write-Host ''
Write-Host '发布完成。' -ForegroundColor Green
Write-Host ("可执行文件：" + (Join-Path $OutputPath 'StockAnalyzer.Desktop.exe'))
Write-Host '提示：首次运行会在 %LOCALAPPDATA%\StockAnalyzer 下创建 stock.db 本地数据库。'
