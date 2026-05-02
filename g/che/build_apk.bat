@echo off
chcp 65001 >nul
echo ========================================
echo   中国象棋 APK 一键构建脚本
echo ========================================
echo.

set JAVA_HOME=C:\Program Files\Eclipse Adoptium\jdk-17.0.18.8-hotspot

if not exist "%JAVA_HOME%\bin\java.exe" (
    echo [错误] 未找到 JDK 17，请先安装：
    echo   winget install EclipseAdoptium.Temurin.17.JDK
    pause
    exit /b 1
)

echo [1/2] 编译 Debug APK...
call gradlew.bat assembleDebug
if %ERRORLEVEL% neq 0 (
    echo [错误] Debug 构建失败！
    pause
    exit /b 1
)

echo.
echo [2/2] 编译 Release APK...
call gradlew.bat assembleRelease
if %ERRORLEVEL% neq 0 (
    echo [错误] Release 构建失败！
    pause
    exit /b 1
)

echo.
echo ========================================
echo   构建成功！
echo ========================================
echo.
echo   Debug APK:   app\build\outputs\apk\debug\app-debug.apk
echo   Release APK: app\build\outputs\apk\release\app-release.apk
echo.
echo   将 APK 文件传到手机即可安装
echo ========================================
pause
