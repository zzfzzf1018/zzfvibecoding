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

1. 在仓库根目录执行（首次需要生成 Gradle Wrapper jar）：
   ```bash
   gradle wrapper --gradle-version 8.5
   ```
2. 调试包：
   ```bash
   ./gradlew assembleDebug
   ```
   APK 输出：`app/build/outputs/apk/debug/app-debug.apk`
3. 安装到设备：
   ```bash
   ./gradlew installDebug
   ```

> 也可以直接用 Android Studio 打开本目录，IDE 会自动同步并生成 wrapper。

## 使用

1. 首次启动进入书架，点击右下角 **+** 选择本地 `.epub` 文件导入。
2. 点击书目进入阅读，按 Kindle 习惯左右点击翻页，顶部点击呼出菜单。
3. 在菜单中选 **朗读** → 开始朗读 / 调整语速。再次点击「朗读」按钮停止。

## 已知限制 & 后续可扩展

- WebView 采用整章纵向滚动模拟翻页（非水平分页动画）。若需更细粒度可改用 `CSS columns` 多列分页。
- TTS 使用本机系统引擎；若需对接在线 AI 语音（Edge TTS / Azure / 火山等），在 `TtsManager` 增加 HTTP 合成 + 流式播放即可。
- 暂未实现：高亮、笔记、字典查询、跨设备同步。
