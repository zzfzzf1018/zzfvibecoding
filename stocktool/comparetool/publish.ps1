# 单文件发布
# 在仓库根目录或 ComparetoolWpf 目录运行：
#   dotnet publish ComparetoolWpf -c Release -r win-x64 --self-contained true `
#       -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true `
#       -p:EnableCompressionInSingleFile=true
# 输出：ComparetoolWpf\bin\Release\net8.0-windows\win-x64\publish\ComparetoolWpf.exe

param(
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64"
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
Push-Location $root
try {
    dotnet publish ComparetoolWpf -c $Configuration -r $Runtime `
        --self-contained true `
        -p:PublishSingleFile=true `
        -p:IncludeNativeLibrariesForSelfExtract=true `
        -p:EnableCompressionInSingleFile=true
    $exe = Join-Path $root "ComparetoolWpf\bin\$Configuration\net8.0-windows\$Runtime\publish\ComparetoolWpf.exe"
    if (Test-Path $exe) {
        Write-Host "`n生成成功: $exe" -ForegroundColor Green
        $size = (Get-Item $exe).Length / 1MB
        Write-Host ("大小: {0:N1} MB" -f $size)
    }
}
finally { Pop-Location }
