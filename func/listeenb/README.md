# ListeenB

ListeenB 是一个原生 Android 读书 App，支持本地导入 EPUB、阅读正文、使用系统 TextToSpeech 听书，并自动保存阅读进度。

## 已实现功能

- 本地选择并导入 `.epub` 文件。
- 解析 EPUB 内的 `container.xml`、OPF manifest/spine 和 XHTML 章节。
- 显示章节正文，支持上一章/下一章切换。
- 使用 Android 系统 TextToSpeech 听书。
- 支持男声/女声偏好设置；实际声线取决于设备安装的 TTS 引擎，找不到匹配声线时会回退默认声音。
- 自动保存最近阅读的书籍 URI、章节、滚动位置和语音偏好。

## 项目结构

- `app/src/main/java/com/listeenb/reader/MainActivity.java`：主界面、EPUB 导入、TTS 和进度恢复。
- `app/src/main/java/com/listeenb/reader/epub/EpubParser.java`：EPUB ZIP/OPF/XHTML 解析。
- `app/src/main/java/com/listeenb/reader/data/ProgressStore.java`：阅读进度持久化。

## 构建要求

- JDK 11 或更高版本。
- Android SDK，建议安装 Android SDK Platform 34 和 Build Tools 34.x。
- Gradle 7.x，或使用 Android Studio 打开项目后由 IDE 管理 Gradle。

当前项目使用 Android Gradle Plugin `7.4.2`，`compileSdk` 为 34。

## 构建 APK

在项目根目录运行：

```powershell
gradle assembleDebug
```

如果你使用 Android Studio，可以直接打开本目录，等待 Gradle Sync 完成后执行 `Build > Build Bundle(s) / APK(s) > Build APK(s)`。

Debug APK 生成位置：

```text
app/build/outputs/apk/debug/app-debug.apk
```

如果当前机器没有安装 Gradle，但已经安装 Android SDK，也可以运行本项目内置的手工构建脚本：

```powershell
.\tools\build-debug-apk.ps1
```

手工构建生成位置：

```text
build/manual/app-debug.apk
```

## 构建 Release APK

使用 Gradle 构建未签名 Release APK：

```powershell
gradle assembleRelease
```

Release APK 默认生成位置：

```text
app/build/outputs/apk/release/app-release-unsigned.apk
```

正式发布到应用商店前，需要使用你自己的 release keystore 签名。可以先生成一个本地签名证书：

```powershell
keytool -genkeypair -v -keystore release.keystore -alias listeenb -keyalg RSA -keysize 2048 -validity 10000
```

然后使用 Android SDK 的 `apksigner` 签名：

```powershell
apksigner sign --ks release.keystore --out app-release-signed.apk app/build/outputs/apk/release/app-release-unsigned.apk
apksigner verify --verbose app-release-signed.apk
```

也可以在 Android Studio 中打开项目，通过 `Build > Generate Signed Bundle / APK` 生成已签名的 Release APK。

注意：`release.keystore`、签名密码和任何 `*.jks` 文件不要提交到 Git 仓库。

## 使用说明

1. 安装 APK 到 Android 设备或模拟器。
2. 点击“导入 EPUB”，选择本地 `.epub` 文件。
3. 使用“上一章”“下一章”阅读。
4. 选择“男声”或“女声”，点击“听书”。
5. 退出后再次打开 App，会自动尝试恢复上次书籍、章节和阅读位置。

## 范围说明

第一版聚焦可用的本地 EPUB 文字阅读体验。暂不支持 DRM EPUB、复杂 CSS 排版、固定版式 EPUB、云同步、在线 AI 语音服务和书架数据库。
