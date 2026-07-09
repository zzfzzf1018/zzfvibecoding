
<#
.SYNOPSIS
    大A ETF工具一键启动脚本 (PowerShell版)
.DESCRIPTION
    自动检查环境、还原依赖、构建并启动应用
.EXAMPLE
    .\start.ps1
#>

Write-Host "==============================================" -ForegroundColor Cyan
Write-Host "           大A ETF工具 - 一键启动脚本" -ForegroundColor Cyan
Write-Host "==============================================" -ForegroundColor Cyan
Write-Host "项目地址: https://github.com/example/etf-tool" -ForegroundColor Gray
Write-Host "文档目录: docs/" -ForegroundColor Gray
Write-Host "  - requirements.md   : 需求文档" -ForegroundColor Gray
Write-Host "  - design.md         : 设计文档" -ForegroundColor Gray
Write-Host "  - ai-guidance.md    : AI开发指导文档" -ForegroundColor Gray
Write-Host "==============================================" -ForegroundColor Cyan
Write-Host ""

# 检查解决方案文件
if (-not (Test-Path "EtfTool.sln")) {
    Write-Host "[错误] 未找到解决方案文件 EtfTool.sln" -ForegroundColor Red
    Write-Host "[提示] 请确保在项目根目录下运行此脚本" -ForegroundColor Yellow
    Read-Host "按 Enter 键退出"
    exit 1
}

# 检查 .NET SDK
Write-Host "[1/5] 正在检查 .NET SDK 是否安装..." -ForegroundColor Green
$dotnetVersion = $null
try {
    $dotnetVersion = dotnet --version 2>&1
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet not found"
    }
}
catch {
    Write-Host "[错误] 未安装 .NET SDK" -ForegroundColor Red
    Write-Host "[提示] 请访问 https://dotnet.microsoft.com/download 安装 .NET 6.0 或更高版本" -ForegroundColor Yellow
    Read-Host "按 Enter 键退出"
    exit 1
}

Write-Host "[完成] .NET SDK 已安装 (版本: $dotnetVersion)" -ForegroundColor Green
Write-Host ""

# 还原依赖
Write-Host "[2/5] 正在还原依赖包..." -ForegroundColor Green
dotnet restore EtfTool.sln --source https://api.nuget.org/v3/index.json
if ($LASTEXITCODE -ne 0) {
    Write-Host "[错误] 依赖包还原失败" -ForegroundColor Red
    Write-Host "[提示] 请检查网络连接或代理设置" -ForegroundColor Yellow
    Read-Host "按 Enter 键退出"
    exit 1
}

Write-Host "[完成] 依赖包还原成功" -ForegroundColor Green
Write-Host ""

# 构建项目
Write-Host "[3/5] 正在构建项目..." -ForegroundColor Green
dotnet build EtfTool.sln --configuration Release --source https://api.nuget.org/v3/index.json
if ($LASTEXITCODE -ne 0) {
    Write-Host "[错误] 项目构建失败" -ForegroundColor Red
    Write-Host "[提示] 请检查代码错误或项目配置" -ForegroundColor Yellow
    Read-Host "按 Enter 键退出"
    exit 1
}

Write-Host "[完成] 项目构建成功" -ForegroundColor Green
Write-Host ""

# 启动应用
Write-Host "[4/5] 正在启动应用..." -ForegroundColor Green
Write-Host "==============================================" -ForegroundColor Cyan
Write-Host ""

dotnet run --project EtfTool.Wpf\EtfTool.Wpf.csproj --configuration Release

Write-Host ""
Write-Host "==============================================" -ForegroundColor Cyan
Write-Host "[5/5] 应用已退出" -ForegroundColor Green
Write-Host "[提示] 如需重新启动，请再次运行此脚本" -ForegroundColor Yellow
Read-Host "按 Enter 键退出"
