<#
.SYNOPSIS
股票分析工具一键启动脚本

.DESCRIPTION
自动检测Python环境，创建虚拟环境，安装依赖并启动股票分析工具
#>

param(
    [Parameter(ValueFromRemainingArguments)]
    $Args
)

$ErrorActionPreference = "Stop"

$VENV_DIR = "venv"
$REQUIREMENTS = "requirements.txt"
$MAIN_SCRIPT = "gui.py"

Write-Host "============================================" -ForegroundColor Cyan
Write-Host "        股票分析工具 - 一键启动脚本" -ForegroundColor Cyan
Write-Host "============================================" -ForegroundColor Cyan
Write-Host ""

function Test-PythonValid {
    param(
        [string]$pythonPath
    )
    if (-not (Test-Path $pythonPath)) {
        return $false
    }
    if ($pythonPath -match "WindowsApps") {
        return $false
    }
    try {
        & $pythonPath --version 2>&1 | Out-Null
        return $LASTEXITCODE -eq 0
    } catch {
        return $false
    }
}

function Find-Python {
    Write-Host "[查找] 正在搜索Python环境..." -ForegroundColor Yellow
    
    Write-Host "[查找] 尝试py启动器..." -ForegroundColor Yellow
    try {
        $py = Get-Command "py" -ErrorAction SilentlyContinue
        if ($py) {
            $version = & $py -3 --version 2>&1
            if ($LASTEXITCODE -eq 0) {
                $executable = & $py -3 -c "import sys; print(sys.executable)" 2>&1
                if ($LASTEXITCODE -eq 0 -and $executable -and (Test-PythonValid $executable)) {
                    Write-Host "[查找] py启动器找到: $version" -ForegroundColor Green
                    return $executable
                }
            }
        }
    } catch {
    }

    Write-Host "[查找] 检查常见安装路径..." -ForegroundColor Yellow
    $installPaths = @(
        "$env:LOCALAPPDATA\Programs\Python\Python3*\python.exe",
        "C:\Python3*\python.exe",
        "C:\Program Files\Python3*\python.exe",
        "C:\Program Files (x86)\Python3*\python.exe"
    )
    foreach ($path in $installPaths) {
        $resolved = Get-ChildItem -Path $path -ErrorAction SilentlyContinue | Select-Object -First 1
        if ($resolved -and (Test-PythonValid $resolved.FullName)) {
            Write-Host "[查找] 在 $($resolved.DirectoryName) 找到" -ForegroundColor Green
            return $resolved.FullName
        }
    }

    Write-Host "[查找] 检查conda环境..." -ForegroundColor Yellow
    $condaPaths = @(
        "$env:USERPROFILE\miniconda3\python.exe",
        "$env:USERPROFILE\anaconda3\python.exe",
        "$env:LOCALAPPDATA\miniconda3\python.exe"
    )
    foreach ($path in $condaPaths) {
        if (Test-Path $path -and (Test-PythonValid $path)) {
            Write-Host "[查找] 在 $path 找到" -ForegroundColor Green
            return $path
        }
    }

    Write-Host "[查找] 通过PATH查找（过滤WindowsApps）..." -ForegroundColor Yellow
    foreach ($cmd in @("python", "python3")) {
        try {
            $python = Get-Command $cmd -ErrorAction SilentlyContinue
            if ($python) {
                $source = $python.Source
                if (Test-PythonValid $source) {
                    Write-Host "[查找] 在PATH中找到: $source" -ForegroundColor Green
                    return $source
                }
            }
        } catch {
        }
    }

    return $null
}

$pythonPath = Find-Python

if (-not $pythonPath) {
    Write-Host ""
    Write-Host "[错误] 未找到有效的Python环境！" -ForegroundColor Red
    Write-Host ""
    Write-Host "检测到的python.exe可能是Microsoft Store的占位程序，" -ForegroundColor Yellow
    Write-Host "无法创建虚拟环境。请按照以下步骤安装Python：" -ForegroundColor Yellow
    Write-Host ""
    Write-Host "1. 访问 https://www.python.org/downloads/"
    Write-Host "2. 下载Python 3.9或更高版本"
    Write-Host "3. 安装时务必勾选 'Add Python to PATH'"
    Write-Host "4. 重新运行此脚本"
    Write-Host ""
    Read-Host "按回车键退出"
    exit 1
}

Write-Host "[OK] 找到Python: $pythonPath" -ForegroundColor Green
Write-Host ""

function Setup-Venv {
    if (Test-Path $VENV_DIR) {
        if (-not (Test-Path (Join-Path $VENV_DIR "Scripts\python.exe"))) {
            Write-Host "[修复] 检测到损坏的虚拟环境，正在删除重建..." -ForegroundColor Yellow
            Remove-Item -Recurse -Force $VENV_DIR
        }
    }
    
    if (-not (Test-Path $VENV_DIR)) {
        Write-Host "[创建] 正在创建虚拟环境..." -ForegroundColor Yellow
        & $pythonPath -m venv $VENV_DIR
        if ($LASTEXITCODE -ne 0) {
            Write-Host "[错误] 创建虚拟环境失败！" -ForegroundColor Red
            Write-Host ""
            Write-Host "可能原因：" -ForegroundColor Yellow
            Write-Host "1. 当前Python版本不支持venv模块"
            Write-Host "2. 权限不足，无法创建目录"
            Write-Host ""
            Read-Host "按回车键退出"
            exit 1
        }
    }
    
    $venvPip = Join-Path $VENV_DIR "Scripts\pip.exe"
    if (-not (Test-Path $venvPip)) {
        Write-Host "[更新] 正在更新pip..." -ForegroundColor Yellow
        & (Join-Path $VENV_DIR "Scripts\python.exe") -m ensurepip
    }
    
    Write-Host "[检查] 正在安装依赖..." -ForegroundColor Yellow
    & $venvPip install -r $REQUIREMENTS
    if ($LASTEXITCODE -ne 0) {
        Write-Host "[警告] 部分依赖安装失败，程序可能无法正常运行" -ForegroundColor Yellow
        Write-Host ""
    }
}

Setup-Venv

Write-Host "============================================" -ForegroundColor Cyan
Write-Host "          启动股票分析工具" -ForegroundColor Cyan
Write-Host "============================================" -ForegroundColor Cyan
Write-Host ""

& (Join-Path $VENV_DIR "Scripts\python.exe") $MAIN_SCRIPT $Args

Write-Host ""
Write-Host "============================================" -ForegroundColor Cyan
Write-Host "               程序已退出" -ForegroundColor Cyan
Write-Host "============================================" -ForegroundColor Cyan
Read-Host "按回车键退出"