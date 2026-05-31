# 林中路阅读器 · Holzwege

> 安静、克制、贴近纸书。一款离线的 Android EPUB 阅读器。
>
> （工程目录、包名、签名仍沿用历史名称 `LisB` / `com.lisb.reader`，仅显示名与 APK 输出名换成「林中路阅读器」/「Holzwege」。）

## 功能

- 📖 **EPUB 解析**：基于 `epub4j` + `jsoup`，章节渲染、目录跳转、内联 CSS/图片
- 🎨 **保留出版社排版** 或 切换 **阅读器默认排版**（字体 / 字号 / 字间距 / 行间距 全可调）
- 👆 **Kindle 式触控 + 滑动翻页**
  - 点击屏幕**右 1/3** 或 **左滑** → 下一页
  - 点击屏幕**左 1/3** 或 **右滑** → 上一页
  - 点击屏幕**上部 / 中部** → 显示/隐藏菜单
- 🌗 主题（白天 / 护眼 / 夜间 / 纯黑）、亮度
- 📑 **常驻 header / footer**：顶部「第 X/N 章 · 当前章页码 · 章节名」，底部「全书进度 % · 本章页码」
- 🔊 **AI 朗读** —— 系统 TTS 引擎
  - 从**当前页**开始朗读（不再每次都从章首）
  - 自动记忆位置；下次朗读弹出「从上次位置 / 从当前页」选择
  - **后台播放** + 通知栏 Media Style 控件（**上一章 / 暂停继续 / 下一章 / 停止**），锁屏控件、蓝牙耳机线控均可用
  - 朗读完本章自动翻章
- 💾 **进度保存**：自动按书保存章节 + 滚动位置 + TTS 位置
- 📚 **本地书架**：导入的 EPUB 缓存到 App 私有目录，长按删除；导入非 EPUB 文件直接拒绝并提示

## 工程结构

```
app/
 └─ src/main/
     ├─ AndroidManifest.xml              # 权限 / 服务 / 接收器
     ├─ java/com/lisb/reader/
     │   ├─ data/SettingsManager.kt      # 设置 + 进度 + TTS 进度 + 书架
     │   ├─ epub/EpubBook.kt             # EPUB 解析 + 资源内联
     │   ├─ tts/
     │   │   ├─ TtsManager.kt            # 系统 TTS 封装（按 chunk 暂停/恢复）
     │   │   ├─ TtsService.kt            # 前台服务 + MediaSession + MediaStyle 通知
     │   │   └─ TtsActionReceiver.kt     # 通知按钮 → 服务
     │   └─ ui/
     │       ├─ BookshelfActivity.kt     # 书架 / 导入校验
     │       ├─ ReaderActivity.kt        # 阅读器（触控 / 滑动 / 菜单 / TTS / indicator）
     │       └─ TouchOverlay.kt          # tap + 水平 swipe 识别
     └─ res/                              # 布局、主题、图标
```

## 构建

需要 **JDK 17** + **Android SDK**（含 Platform 36、Build-Tools 36.0.0）。Gradle / AGP 用工程已固化的版本（Gradle 8.10.2 / AGP 8.7.3 / Kotlin 1.9.24）。

### 一键 PowerShell 脚本（推荐）

```powershell
cd d:\zzf\flyingdev\zzfvibecoding\func\lisB

# 普通构建
.\build-release.ps1

# 先 clean 再构建
.\build-release.ps1 -Clean

# 用 debug 签名快速出包
.\build-release.ps1 -NoSign
```

脚本会：

1. 校验 `JAVA_HOME` / `ANDROID_HOME`（找不到时会引导）
2. 没有 `gradlew.bat` 时自动生成 Gradle Wrapper
3. 首次运行用 `keytool` 生成 `build/keystores/lisb-release.jks`，写入 `keystore.properties`（被 `.gitignore` 排除）。`app/build.gradle.kts` 自动读取它作 release 签名
4. 执行 `.\gradlew.bat assembleRelease`
5. 把产物复制到 `dist\Holzwege-<versionName>-release-<时间戳>.apk` 并打印安装命令

安装：

```powershell
adb install -r .\dist\Holzwege-1.0.0-release-xxxx.apk
```

> ⚠️ 首次签名 keystore 请**妥善备份**。后续升级安装必须使用同一个 keystore。

### 手动 Gradle

```powershell
.\gradlew.bat assembleRelease
# APK: app/build/outputs/apk/release/app-release.apk
```

## 排版模式

菜单栏 → **字体** 对话框底部有「**保留电子书自带排版**」开关：

- ✅ **开启（默认）**：保留出版社原始 CSS（字体 / 段落 / 章节标题等），仅叠加主题底色与图片自适应宽度。App 内的字体 / 字号 / 字距 / 行距设置在此模式下**不生效**。
- ⬜ **关闭**：丢弃 EPUB 自带 CSS，使用阅读器默认排版：
  - 字体（宋 / 黑 / 系统）
  - 字号（12–40 px）
  - **字间距（0.00–0.30 em）**
  - **行间距（1.0–3.0）**

## 朗读使用

1. 菜单栏 → **朗读** → 调语速 → **开始朗读**
2. 若上次保存的位置与当前页不同，会弹「**从上次位置 / 从当前页**」二选一
3. 通知栏出现 Media Style 控件：**上一章 / ▶︎❚❚ / 下一章 / 停止**
4. 锁屏后或退到后台，仍可控制；蓝牙耳机的 Play / Pause / Next 按键也已映射

## 已知限制

- WebView 整章纵向滚动模拟翻页（非水平分页动画）
- 保留原排版模式下，App 字体 / 字号 / 字距 / 行距设置不生效（让位给原书 CSS）
- TTS 使用本机系统引擎；若需在线 AI 语音（Edge TTS / Azure / 火山等），在 `TtsManager` / `TtsService` 加 HTTP 合成 + 流式播放即可
- 暂未实现：高亮、笔记、字典查询、跨设备同步
