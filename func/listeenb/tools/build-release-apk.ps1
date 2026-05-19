param(
    [string]$AndroidSdk = "C:\Program Files (x86)\Android\android-sdk",
    [string]$PlatformVersion = "android-36",
    [string]$BuildToolsVersion = "36.0.0",
    [string]$KeystorePath = "",
    [string]$KeystoreAlias = "listeenb-release",
    [string]$KeystorePassword = "android",
    [string]$KeyPassword = "android"
)

$ErrorActionPreference = "Stop"

$root = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
$platform = Join-Path $AndroidSdk "platforms\$PlatformVersion\android.jar"
$buildTools = Join-Path $AndroidSdk "build-tools\$BuildToolsVersion"
$out = Join-Path $root "build\manual-release"
$compiled = Join-Path $out "compiled"
$generated = Join-Path $out "gen"
$classes = Join-Path $out "classes"
$dex = Join-Path $out "dex"
$defaultKeystorePath = Join-Path $root "build\keystores\release.keystore"

if (!(Test-Path $platform)) {
    throw "Android platform jar not found: $platform"
}

$aapt2 = Join-Path $buildTools "aapt2.exe"
$d8 = Join-Path $buildTools "d8.bat"
$zipalign = Join-Path $buildTools "zipalign.exe"
$apksigner = Join-Path $buildTools "apksigner.bat"

foreach ($tool in @($aapt2, $d8, $zipalign, $apksigner)) {
    if (!(Test-Path $tool)) {
        throw "Required Android build tool not found: $tool"
    }
}

function Invoke-Checked {
    param(
        [string]$FilePath,
        [string[]]$Arguments
    )
    & $FilePath @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "Command failed ($LASTEXITCODE): $FilePath $($Arguments -join ' ')"
    }
}

if ([string]::IsNullOrWhiteSpace($KeystorePath)) {
    $KeystorePath = $defaultKeystorePath
}

Remove-Item $out -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force $compiled, $generated, $classes, $dex | Out-Null

Push-Location $root
try {
    Invoke-Checked $aapt2 @("compile", "--dir", "app\src\main\res", "-o", $compiled)
    $flatFiles = Get-ChildItem $compiled -Filter "*.flat" | ForEach-Object { $_.FullName }
    Invoke-Checked $aapt2 (@("link", "-I", $platform, "--manifest", "app\src\main\AndroidManifest.xml", "--java", $generated, "-o", (Join-Path $out "resources.apk")) + $flatFiles)

    $sourceFiles = @()
    $sourceFiles += Get-ChildItem "app\src\main\java" -Recurse -Filter "*.java" | ForEach-Object { $_.FullName }
    $sourceFiles += Get-ChildItem $generated -Recurse -Filter "*.java" | ForEach-Object { $_.FullName }
    Invoke-Checked "javac" (@("-source", "1.8", "-target", "1.8", "-encoding", "UTF-8", "-cp", $platform, "-d", $classes) + $sourceFiles)

    $classFiles = Get-ChildItem $classes -Recurse -Filter "*.class" | ForEach-Object { $_.FullName }
    Invoke-Checked $d8 (@("--classpath", $platform, "--min-api", "23", "--release", "--output", $dex) + $classFiles)
    $classesDex = Join-Path $dex "classes.dex"
    if (!(Test-Path $classesDex)) {
        throw "D8 did not generate classes.dex"
    }

    $unsignedApk = Join-Path $out "app-release-unsigned.apk"
    $alignedApk = Join-Path $out "app-release-aligned.apk"
    $releaseApk = Join-Path $out "app-release.apk"
    Copy-Item (Join-Path $out "resources.apk") $unsignedApk -Force
    Invoke-Checked "jar" @("uf", $unsignedApk, "-C", $dex, "classes.dex")
    Invoke-Checked $zipalign @("-f", "4", $unsignedApk, $alignedApk)

    if (!(Test-Path $KeystorePath)) {
        New-Item -ItemType Directory -Force (Split-Path -Parent $KeystorePath) | Out-Null
        Invoke-Checked "keytool" @("-genkeypair", "-v", "-keystore", $KeystorePath, "-storepass", $KeystorePassword, "-alias", $KeystoreAlias, "-keypass", $KeyPassword, "-keyalg", "RSA", "-keysize", "2048", "-validity", "10000", "-dname", "CN=ListeenB Release,O=ListeenB,C=CN")
    }

    Invoke-Checked $apksigner @("sign", "--ks", $KeystorePath, "--ks-key-alias", $KeystoreAlias, "--ks-pass", "pass:$KeystorePassword", "--key-pass", "pass:$KeyPassword", "--out", $releaseApk, $alignedApk)
    Invoke-Checked $apksigner @("verify", "--verbose", $releaseApk)
    Write-Host "Release APK generated: $releaseApk"
} finally {
    Pop-Location
}