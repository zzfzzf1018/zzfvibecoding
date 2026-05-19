<#
.SYNOPSIS
    一键编译 LisB Release APK。

.DESCRIPTION
    流程：
      1. 校验 JAVA_HOME / ANDROID_HOME
      2. 若没有 gradlew.bat 则用本地 gradle 生成 wrapper
      3. 若没有 keystore.properties 则自动生成本地签名 keystore（默认放在 build/keystores/）
      4. 执行 .\gradlew.bat assembleRelease
      5. 把产物复制到 dist/LisB-<version>-release.apk

.PARAMETER Clean
    构建前先执行 clean。

.PARAMETER NoSign
    跳过自动生成签名 keystore（构建仍会进行——AGP 会回退到 debug 签名）。

.EXAMPLE
    .\build-release.ps1
    .\build-release.ps1 -Clean
#>

[CmdletBinding()]
param(
    [switch]$Clean,
    [switch]$NoSign
)

$ErrorActionPreference = 'Stop'
Set-Location -Path $PSScriptRoot

function Write-Step($msg) { Write-Host "==> $msg" -ForegroundColor Cyan }
function Write-Ok  ($msg) { Write-Host "✓  $msg" -ForegroundColor Green }
function Write-Warn2($msg) { Write-Host "!  $msg" -ForegroundColor Yellow }
function Fail($msg) { Write-Host "✗  $msg" -ForegroundColor Red; exit 1 }

# --- 1. 环境校验 ---------------------------------------------------------
Write-Step "校验构建环境"

if (-not $env:JAVA_HOME) {
    Fail "未设置 JAVA_HOME，请安装 JDK 17 并配置环境变量。"
}
$javaExe = Join-Path $env:JAVA_HOME 'bin\java.exe'
if (-not (Test-Path $javaExe)) { Fail "找不到 $javaExe" }
$javaVer = & $javaExe -version 2>&1 | Select-Object -First 1
Write-Ok "JDK: $javaVer"

if (-not $env:ANDROID_HOME -and -not $env:ANDROID_SDK_ROOT) {
    Fail "未设置 ANDROID_HOME / ANDROID_SDK_ROOT，请先安装 Android SDK。"
}
$sdkRoot = if ($env:ANDROID_HOME) { $env:ANDROID_HOME } else { $env:ANDROID_SDK_ROOT }
Write-Ok "Android SDK: $sdkRoot"

# --- 2. Gradle Wrapper ---------------------------------------------------
$gradlew = Join-Path $PSScriptRoot 'gradlew.bat'
if (-not (Test-Path $gradlew)) {
    Write-Step "未找到 gradlew.bat，尝试用本地 gradle 生成 wrapper"
    $gradleCmd = Get-Command gradle -ErrorAction SilentlyContinue
    if (-not $gradleCmd) {
        Fail "本地未安装 gradle。请安装 Gradle 8.5+ 或用 Android Studio 打开一次本工程后再运行。"
    }
    & gradle wrapper --gradle-version 8.5
    if (-not (Test-Path $gradlew)) { Fail "Wrapper 生成失败。" }
    Write-Ok "已生成 gradlew.bat"
} else {
    Write-Ok "gradlew.bat 已存在"
}

# --- 3. 签名 keystore ----------------------------------------------------
$keystorePropsPath = Join-Path $PSScriptRoot 'keystore.properties'
if ($NoSign) {
    Write-Warn2 "-NoSign 已指定，跳过 keystore 配置（将使用 debug 签名）。"
} elseif (-not (Test-Path $keystorePropsPath)) {
    Write-Step "首次构建：自动生成本地签名 keystore"

    $keystoreDir = Join-Path $PSScriptRoot 'build\keystores'
    New-Item -ItemType Directory -Force -Path $keystoreDir | Out-Null
    $keystoreFile = Join-Path $keystoreDir 'lisb-release.jks'

    $alias = 'lisb'
    # 本地脚本签名 keystore；如需上架商店请改用更强口令并妥善备份
    $storePass = 'lisb-' + ([guid]::NewGuid().ToString('N').Substring(0,12))
    $keyPass   = $storePass

    $keytool = Join-Path $env:JAVA_HOME 'bin\keytool.exe'
    if (-not (Test-Path $keytool)) { Fail "找不到 keytool: $keytool" }

    & $keytool -genkeypair -v `
        -keystore $keystoreFile `
        -storetype PKCS12 `
        -alias $alias `
        -keyalg RSA -keysize 2048 -validity 10000 `
        -storepass $storePass -keypass $keyPass `
        -dname "CN=LisB,OU=LisB,O=LisB,L=Local,S=Local,C=CN"
    if ($LASTEXITCODE -ne 0) { Fail "keytool 生成失败。" }

    # keystore.properties 中的路径使用相对路径（相对工程根目录）
    $relStore = (Resolve-Path $keystoreFile -Relative).Replace('\','/').TrimStart('.','/')
    $relStore = "build/keystores/lisb-release.jks"

    @(
        "storeFile=$relStore",
        "storePassword=$storePass",
        "keyAlias=$alias",
        "keyPassword=$keyPass"
    ) | Set-Content -Path $keystorePropsPath -Encoding ASCII

    Write-Ok "Keystore 已生成：$keystoreFile"
    Write-Warn2 "请妥善保存 keystore.properties / *.jks（已被 .gitignore 排除）。"
} else {
    Write-Ok "复用已有 keystore.properties"
}

# --- 4. 构建 -------------------------------------------------------------
if ($Clean) {
    Write-Step "执行 gradlew clean"
    & $gradlew clean
    if ($LASTEXITCODE -ne 0) { Fail "clean 失败。" }
}

Write-Step "开始构建 Release"
& $gradlew --no-daemon assembleRelease
if ($LASTEXITCODE -ne 0) { Fail "assembleRelease 失败。" }
Write-Ok "构建完成"

# --- 5. 输出 -------------------------------------------------------------
$apkDir = Join-Path $PSScriptRoot 'app\build\outputs\apk\release'
$apk = Get-ChildItem -Path $apkDir -Filter '*.apk' -File -ErrorAction SilentlyContinue | Select-Object -First 1
if (-not $apk) { Fail "未找到 release APK：$apkDir" }

$distDir = Join-Path $PSScriptRoot 'dist'
New-Item -ItemType Directory -Force -Path $distDir | Out-Null

# 解析 versionName 用于命名（取 build.gradle.kts 中第一个 versionName = "x.y.z"）
$gradleFile = Join-Path $PSScriptRoot 'app\build.gradle.kts'
$verMatch = Select-String -Path $gradleFile -Pattern 'versionName\s*=\s*"([^"]+)"' | Select-Object -First 1
$version = if ($verMatch) { $verMatch.Matches[0].Groups[1].Value } else { 'unknown' }
$stamp = Get-Date -Format 'yyyyMMdd-HHmm'
$dest = Join-Path $distDir "LisB-$version-release-$stamp.apk"
Copy-Item -Path $apk.FullName -Destination $dest -Force

Write-Host ""
Write-Host "==================== 完成 ====================" -ForegroundColor Green
Write-Host "源 APK : $($apk.FullName)"
Write-Host "副本   : $dest"
Write-Host "大小   : $([math]::Round($apk.Length / 1MB, 2)) MB"
Write-Host "安装   : adb install -r `"$dest`""
Write-Host "=============================================="
