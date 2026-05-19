# LisB —— 极简 EPUB 阅读器

一款类 Kindle 触感的 Android EPUB 阅读器。

## 功能

- 📖 **EPUB 解析**：基于 `epublib` + `jsoup`，支持章节渲染、目录跳转
- 👆 **Kindle 式触控**
  - 点击屏幕**右侧 1/3** → 下一页
  - 点击屏幕**左侧 1/3** → 上一页
  - 点击屏幕**上部 / 中部** → 显示/隐藏菜单栏
- 🎨 **菜单栏**：字体（宋/黑/系统）、字号（12–40px）、主题（白天/护眼/夜间/纯黑）、亮度、目录
- 🔊 **AI 朗读**：调用系统 TTS 引擎朗读当前章节，结束后**自动翻到下一章**，可调语速
- 💾 **进度保存**：自动按书保存 章节 + 滚动位置（`SharedPreferences`），打开即续读
- 📚 **本地书架**：导入的 EPUB 缓存到 App 私有目录，长按删除

## 工程结构

```
app/
 ├─ build.gradle.kts          # 依赖、构建配置
 └─ src/main/
     ├─ AndroidManifest.xml
     ├─ java/com/lisb/reader/
     │   ├─ LisBApp.kt
     │   ├─ data/SettingsManager.kt    # 设置 + 进度 + 书架
     │   ├─ epub/EpubBook.kt           # EPUB 解析
     │   ├─ tts/TtsManager.kt          # 朗读 + 自动翻页
     │   └─ ui/
     │       ├─ BookshelfActivity.kt   # 书架 / 导入
     │       └─ ReaderActivity.kt      # 阅读器（触控/菜单/TTS）
     └─ res/                            # 布局、主题、图标
```

## 构建

需要 **Android Studio Iguana+** 或 命令行 Gradle 8.5+ / Android Gradle Plugin 8.2+ / JDK 17。

### 方式 A：一键 PowerShell 脚本（推荐）

仓库自带 [build-release.ps1](build-release.ps1)，可一键完成「生成 Wrapper → 生成本地签名 keystore → 编译 Release → 归档 APK 到 dist/」。

前置：
- 安装 **JDK 17** 并设置 `JAVA_HOME`
- 安装 **Android SDK**（含 Platform 34、Build-Tools 34.x）并设置 `ANDROID_HOME`（或 `ANDROID_SDK_ROOT`）
- 首次使用如还没有 `gradlew.bat`，脚本会尝试调用系统已安装的 `gradle` 自动生成。若没装 Gradle，最简单的办法是先用 **Android Studio** 打开本工程让 IDE 同步一次。

用法：

```powershell
cd d:\zzf\flyingdev\zzfvibecoding\func\lisB

# 普通构建
.\build-release.ps1

# 先 clean 再构建
.\build-release.ps1 -Clean

# 跳过签名 keystore 自动生成（用 debug 签名出包，便于本地快速测试）
.\build-release.ps1 -NoSign
```

脚本行为：

1. 校验 `JAVA_HOME` / `ANDROID_HOME`。
2. 没有 `gradlew.bat` 时调用 `gradle wrapper --gradle-version 8.5` 自举。
3. 首次运行用 `keytool` 生成 `build/keystores/lisb-release.jks`，并写入根目录 `keystore.properties`（已被 `.gitignore` 排除）。`app/build.gradle.kts` 会自动读取它作为 release 签名。
4. 执行 `.\gradlew.bat assembleRelease`。
5. 把产物复制到 `dist\LisB-<versionName>-release-<时间戳>.apk` 并打印安装命令。

安装到设备：

```powershell
adb install -r .\dist\LisB-1.0.0-release-xxxx.apk
```

> ⚠️ 首次签名生成的 keystore 请**妥善备份**。日后用同一身份升级安装必须使用同一个 keystore，丢失则只能卸载重装。如要上架商店，请自行管理更安全的 keystore，而不是用脚本随机生成的那一份。

### 方式 B：手动 Gradle

1. 在仓库根目录执行（首次需要生成 Gradle Wrapper jar）：
   ```powershell
   gradle wrapper --gradle-version 8.5
   ```
2. 构建：
   ```powershell
   .\gradlew.bat assembleRelease   # 或 assembleDebug
   ```
   APK 输出：`app/build/outputs/apk/release/app-release.apk`
3. 安装到设备：
   ```powershell
   .\gradlew.bat installRelease
   ```

> 也可以直接用 Android Studio 打开本目录，IDE 会自动同步并生成 wrapper。

## 排版模式

菜单栏 → **字体** 对话框底部有「**保留电子书自带排版**」开关：

- ✅ **开启（默认）**：解析时把章节的 `<head>`、`<style>`、`<link rel="stylesheet">` 和 `<img>` 全部内联进 WebView 文档，**完整保留出版社的字体、段落、引文、章节标题样式**。仅叠加一层主题颜色（白天/护眼/夜间/纯黑）作为背景与文字色覆盖。此模式下 App 内的字体 / 字号设置不生效（让位给原书排版）。
- ⬜ **关闭**：丢弃 EPUB 自带 CSS，使用阅读器默认排版（统一字体、字号、行高、首行缩进），适合排版很糟糕或想要全局统一观感的旧书。

## 使用

1. 首次启动进入书架，点击右下角 **+** 选择本地 `.epub` 文件导入。
2. 点击书目进入阅读，按 Kindle 习惯左右点击翻页，顶部点击呼出菜单。
3. 在菜单中选 **朗读** → 开始朗读 / 调整语速。再次点击「朗读」按钮停止。

## 已知限制 & 后续可扩展

- WebView 采用整章纵向滚动模拟翻页（非水平分页动画）。若需更细粒度可改用 `CSS columns` 多列分页。
- 在「保留原排版」模式下，App 的字体/字号设置不生效（让位给原书 CSS）。如需强制覆盖字号，可在 `ReaderActivity.buildHtml` 的 overlay 中加 `body{font-size:${size}px !important}`。
- CSS 内 `url(...)` 引用的字体 / 背景图未做二次内联，少数书的自定义 webfont 不会加载。
- TTS 使用本机系统引擎；若需对接在线 AI 语音（Edge TTS / Azure / 火山等），在 `TtsManager` 增加 HTTP 合成 + 流式播放即可。
- 暂未实现：高亮、笔记、字典查询、跨设备同步。
