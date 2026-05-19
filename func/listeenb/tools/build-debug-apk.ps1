param(
    [string]$AndroidSdk = "C:\Program Files (x86)\Android\android-sdk",
    [string]$PlatformVersion = "android-36",
    [string]$BuildToolsVersion = "36.0.0"
)

$ErrorActionPreference = "Stop"

$root = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
$platform = Join-Path $AndroidSdk "platforms\$PlatformVersion\android.jar"
$buildTools = Join-Path $AndroidSdk "build-tools\$BuildToolsVersion"
$out = Join-Path $root "build\manual"
$compiled = Join-Path $out "compiled"
$generated = Join-Path $out "gen"
$classes = Join-Path $out "classes"
$dex = Join-Path $out "dex"

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

Remove-Item $out -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force $compiled, $generated, $classes, $dex | Out-Null

Push-Location $root
try {
    & $aapt2 compile --dir "app\src\main\res" -o $compiled
    $flatFiles = Get-ChildItem $compiled -Filter "*.flat" | ForEach-Object { $_.FullName }
    & $aapt2 link -I $platform --manifest "app\src\main\AndroidManifest.xml" --java $generated -o (Join-Path $out "resources.apk") $flatFiles

    $sourceFiles = @()
    $sourceFiles += Get-ChildItem "app\src\main\java" -Recurse -Filter "*.java" | ForEach-Object { $_.FullName }
    $sourceFiles += Get-ChildItem $generated -Recurse -Filter "*.java" | ForEach-Object { $_.FullName }
    & javac -source 1.8 -target 1.8 -encoding UTF-8 -cp $platform -d $classes $sourceFiles

    $classFiles = Get-ChildItem $classes -Recurse -Filter "*.class" | ForEach-Object { $_.FullName }
    & $d8 --classpath $platform --min-api 23 --output $dex $classFiles
    $classesDex = Join-Path $dex "classes.dex"
    if (!(Test-Path $classesDex)) {
        throw "D8 did not generate classes.dex"
    }

    $unsignedApk = Join-Path $out "app-unsigned.apk"
    $alignedApk = Join-Path $out "app-aligned.apk"
    $debugApk = Join-Path $out "app-debug.apk"
    Copy-Item (Join-Path $out "resources.apk") $unsignedApk -Force
    & jar uf $unsignedApk -C $dex "classes.dex"
    & $zipalign -f 4 $unsignedApk $alignedApk

    $keystore = Join-Path $out "debug.keystore"
    if (!(Test-Path $keystore)) {
        & keytool -genkeypair -v -keystore $keystore -storepass android -alias androiddebugkey -keypass android -keyalg RSA -keysize 2048 -validity 10000 -dname "CN=Android Debug,O=Android,C=US"
    }

    & $apksigner sign --ks $keystore --ks-pass pass:android --key-pass pass:android --out $debugApk $alignedApk
    & $apksigner verify --verbose $debugApk
    Write-Host "APK generated: $debugApk"
} finally {
    Pop-Location
}