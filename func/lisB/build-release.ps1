<#
.SYNOPSIS
    One-click LisB Android Release APK builder.

.DESCRIPTION
    Steps:
      1. Verify JAVA_HOME / ANDROID_HOME.
      2. If gradlew.bat is missing, bootstrap it via local 'gradle'.
      3. If keystore.properties is missing, generate a local signing
         keystore (under build/keystores/) via keytool.
      4. Run .\gradlew.bat assembleRelease.
      5. Copy the APK to dist\LisB-<version>-release-<stamp>.apk.

.PARAMETER Clean
    Run 'gradlew clean' before building.

.PARAMETER NoSign
    Skip auto-generating the release keystore (build will fall back
    to the debug signing config so the APK is still installable).

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
function Write-Ok  ($msg) { Write-Host "[OK] $msg" -ForegroundColor Green }
function Write-Warn2($msg){ Write-Host "[!]  $msg" -ForegroundColor Yellow }
function Fail($msg) { Write-Host "[ERROR] $msg" -ForegroundColor Red; exit 1 }

# --- 1. Environment checks --------------------------------------------
Write-Step "Checking build environment"

if (-not $env:JAVA_HOME) {
    Fail "JAVA_HOME is not set. Please install JDK 17 and set JAVA_HOME."
}
$javaExe = Join-Path $env:JAVA_HOME 'bin\java.exe'
if (-not (Test-Path $javaExe)) { Fail "java.exe not found at $javaExe" }
# Use cmd.exe to merge stderr so PowerShell doesn't wrap lines as ErrorRecord.
$javaVer = (& cmd.exe /c "`"$javaExe`" -version 2>&1") | Select-Object -First 1
Write-Ok "JDK: $javaVer"

# --- Locate Android SDK ------------------------------------------------
function Test-SdkRoot([string]$p) {
    if (-not $p) { return $false }
    if (-not (Test-Path $p)) { return $false }
    # An Android SDK root should contain at least one of these
    foreach ($sub in 'platform-tools', 'platforms', 'cmdline-tools', 'build-tools', 'tools') {
        if (Test-Path (Join-Path $p $sub)) { return $true }
    }
    return $false
}

function Find-AndroidSdk {
    # 1. Environment variables
    foreach ($p in @($env:ANDROID_HOME, $env:ANDROID_SDK_ROOT)) {
        if (Test-SdkRoot $p) { return $p }
    }

    # 2. Common install locations
    $common = @(
        (Join-Path $env:LOCALAPPDATA 'Android\Sdk'),
        (Join-Path $env:LOCALAPPDATA 'Android\sdk'),
        (Join-Path $env:USERPROFILE  'AppData\Local\Android\Sdk'),
        (Join-Path $env:APPDATA      '..\Local\Android\Sdk'),
        'C:\Android\Sdk', 'C:\Android\sdk',
        'C:\Program Files\Android\Sdk',
        'C:\Program Files (x86)\Android\android-sdk',
        'D:\Android\Sdk', 'D:\Android\sdk',
        'E:\Android\Sdk'
    )
    foreach ($p in $common) {
        if (Test-SdkRoot $p) { return (Resolve-Path $p).Path }
    }

    # 3. Derive from tools on PATH (adb, sdkmanager)
    $adb = Get-Command adb.exe -ErrorAction SilentlyContinue
    if ($adb) {
        $cand = Split-Path (Split-Path $adb.Source -Parent) -Parent   # ...\platform-tools\adb.exe -> ...
        if (Test-SdkRoot $cand) { return $cand }
    }
    foreach ($n in 'sdkmanager.bat','sdkmanager') {
        $sm = Get-Command $n -ErrorAction SilentlyContinue
        if ($sm) {
            # ...\cmdline-tools\latest\bin\sdkmanager(.bat) -> 3 levels up
            $cand = Split-Path (Split-Path (Split-Path $sm.Source -Parent) -Parent) -Parent
            if (Test-SdkRoot $cand) { return $cand }
        }
    }

    # 4. Registry (Android Studio installer writes these on some versions)
    foreach ($key in 'HKCU:\SOFTWARE\Android Studio','HKLM:\SOFTWARE\Android Studio') {
        try {
            $v = (Get-ItemProperty -Path $key -ErrorAction Stop).SdkPath
            if (Test-SdkRoot $v) { return $v }
        } catch {}
    }

    # 5. Existing local.properties
    $lp = Join-Path $PSScriptRoot 'local.properties'
    if (Test-Path $lp) {
        $line = Select-String -Path $lp -Pattern '^sdk\.dir=(.+)$' | Select-Object -First 1
        if ($line) {
            $p = $line.Matches[0].Groups[1].Value
            # Reverse the property-file escaping: \\ -> \, \: -> :
            $p = $p -replace '\\\\', '\' -replace '\\:', ':'
            if (Test-SdkRoot $p) { return $p }
        }
    }

    return $null
}

$sdkRoot = Find-AndroidSdk
if (-not $sdkRoot) {
    Write-Warn2 "Could not auto-detect Android SDK in env vars, common paths, PATH, or registry."
    Write-Host "Tip: in Android Studio, go to Settings -> Languages & Frameworks -> Android SDK to see the path."
    $userPath = Read-Host "Enter Android SDK root path (or press Enter to abort)"
    if ($userPath) { $userPath = $userPath.Trim('"').Trim() }
    if ($userPath -and (Test-SdkRoot $userPath)) {
        $sdkRoot = (Resolve-Path $userPath).Path
    } else {
        if ($userPath) { Write-Host "Path '$userPath' does not look like an Android SDK root." -ForegroundColor Red }
        Fail @"
Android SDK not found. Please do one of:
  1. Install Android Studio, open this project once (it will write local.properties), then rerun.
  2. Install the command-line SDK and set ANDROID_HOME to its root, e.g.:
       setx ANDROID_HOME "$env:LOCALAPPDATA\Android\Sdk"
     (restart the shell after setx).
  3. Manually create local.properties in the project root with a single line:
       sdk.dir=C\:\\path\\to\\Android\\Sdk
"@
    }
}
Write-Ok "Android SDK: $sdkRoot"

# Always write local.properties pointing at the detected SDK so Gradle finds it.
$localProps = Join-Path $PSScriptRoot 'local.properties'
$escaped = $sdkRoot -replace '\\', '\\\\' -replace ':', '\:'
"sdk.dir=$escaped" | Set-Content -Path $localProps -Encoding ASCII
Write-Ok "Wrote local.properties (sdk.dir=$sdkRoot)"

# --- 2. Gradle wrapper -------------------------------------------------
$gradlew = Join-Path $PSScriptRoot 'gradlew.bat'
$wrapperJar = Join-Path $PSScriptRoot 'gradle\wrapper\gradle-wrapper.jar'

function Get-WrapperFile([string]$url, [string]$dest) {
    Write-Host "  downloading $url"
    New-Item -ItemType Directory -Force -Path (Split-Path $dest -Parent) | Out-Null
    Invoke-WebRequest -Uri $url -OutFile $dest -UseBasicParsing
}

if (-not (Test-Path $gradlew) -or -not (Test-Path $wrapperJar)) {
    Write-Step "Gradle wrapper missing, fetching from gradle/gradle on GitHub"
    $ref = 'v8.10.2'  # keep in sync with gradle-wrapper.properties
    $base = "https://raw.githubusercontent.com/gradle/gradle/$ref"
    try {
        Get-WrapperFile "$base/gradle/wrapper/gradle-wrapper.jar" $wrapperJar
        if (-not (Test-Path $gradlew)) {
            Get-WrapperFile "$base/gradlew.bat" $gradlew
            Get-WrapperFile "$base/gradlew"     (Join-Path $PSScriptRoot 'gradlew')
        }
    } catch {
        Fail "Failed to download Gradle wrapper: $($_.Exception.Message). Check your internet connection or open the project once in Android Studio."
    }
    Write-Ok "Wrapper installed."
} else {
    Write-Ok "gradlew.bat + wrapper jar present."
}

# --- 3. Signing keystore ----------------------------------------------
$keystorePropsPath = Join-Path $PSScriptRoot 'keystore.properties'
if ($NoSign) {
    Write-Warn2 "-NoSign specified, skipping keystore (debug signing will be used)."
}
elseif (-not (Test-Path $keystorePropsPath)) {
    Write-Step "First run: generating local signing keystore"

    $keystoreDir = Join-Path $PSScriptRoot 'build\keystores'
    New-Item -ItemType Directory -Force -Path $keystoreDir | Out-Null
    $keystoreFile = Join-Path $keystoreDir 'lisb-release.jks'

    $alias     = 'lisb'
    $storePass = 'lisb-' + ([guid]::NewGuid().ToString('N').Substring(0,12))
    $keyPass   = $storePass

    $keytool = Join-Path $env:JAVA_HOME 'bin\keytool.exe'
    if (-not (Test-Path $keytool)) { Fail "keytool not found at $keytool" }

    & $keytool -genkeypair -v `
        -keystore $keystoreFile `
        -storetype PKCS12 `
        -alias $alias `
        -keyalg RSA -keysize 2048 -validity 10000 `
        -storepass $storePass -keypass $keyPass `
        -dname "CN=LisB,OU=LisB,O=LisB,L=Local,S=Local,C=CN"
    if ($LASTEXITCODE -ne 0) { Fail "keytool failed." }

    $relStore = 'build/keystores/lisb-release.jks'
    @(
        "storeFile=$relStore",
        "storePassword=$storePass",
        "keyAlias=$alias",
        "keyPassword=$keyPass"
    ) | Set-Content -Path $keystorePropsPath -Encoding ASCII

    Write-Ok "Keystore created: $keystoreFile"
    Write-Warn2 "Back up keystore.properties and the .jks file (both are gitignored)."
}
else {
    Write-Ok "Reusing existing keystore.properties."
}

# --- 4. Build ----------------------------------------------------------
if ($Clean) {
    Write-Step "gradlew clean"
    & $gradlew clean
    if ($LASTEXITCODE -ne 0) { Fail "clean failed." }
}

Write-Step "Building release"
& $gradlew --no-daemon assembleRelease
if ($LASTEXITCODE -ne 0) { Fail "assembleRelease failed." }
Write-Ok "Build finished."

# --- 5. Publish artifact ----------------------------------------------
$apkDir = Join-Path $PSScriptRoot 'app\build\outputs\apk\release'
$apk = Get-ChildItem -Path $apkDir -Filter '*.apk' -File -ErrorAction SilentlyContinue | Select-Object -First 1
if (-not $apk) { Fail "Release APK not found under $apkDir" }

$distDir = Join-Path $PSScriptRoot 'dist'
New-Item -ItemType Directory -Force -Path $distDir | Out-Null

$gradleFile = Join-Path $PSScriptRoot 'app\build.gradle.kts'
$verMatch = Select-String -Path $gradleFile -Pattern 'versionName\s*=\s*"([^"]+)"' | Select-Object -First 1
$version = if ($verMatch) { $verMatch.Matches[0].Groups[1].Value } else { 'unknown' }
$stamp = Get-Date -Format 'yyyyMMdd-HHmm'
$dest = Join-Path $distDir ("LisB-{0}-release-{1}.apk" -f $version, $stamp)
Copy-Item -Path $apk.FullName -Destination $dest -Force

Write-Host ""
Write-Host "==================== DONE ====================" -ForegroundColor Green
Write-Host ("Source APK : {0}" -f $apk.FullName)
Write-Host ("Archived   : {0}" -f $dest)
Write-Host ("Size       : {0} MB" -f ([math]::Round($apk.Length / 1MB, 2)))
Write-Host ("Install    : adb install -r `"{0}`"" -f $dest)
Write-Host "=============================================="
