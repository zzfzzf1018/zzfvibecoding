# SKILL: 林中路阅读器 (Holzwege EPUB Reader)

## 项目身份

- **名称**: 林中路 / Holzwege Reader
- **类型**: Android EPUB 阅读器
- **包名**: `com.lisb.reader`
- **路径**: `d:\zzf\flyingdev\zzfvibecoding\func\lisB`
- **当前版本**: v1.6.0 (versionCode=10)

## 快速上手

### 构建

```powershell
cd d:\zzf\flyingdev\zzfvibecoding\func\lisB
.\build-release.ps1
```

输出: `dist/Holzwege-{version}-release-{timestamp}.apk`

### 环境要求

- JDK 17 (JAVA_HOME)
- Android SDK (自动检测)
- Windows PowerShell 5.1+

### 关键文件

| 文件 | 作用 | 重要度 |
|------|------|--------|
| `app/src/main/java/com/lisb/reader/ui/ReaderActivity.kt` | 阅读器主逻辑 + READER_JS | ⭐⭐⭐ |
| `app/src/main/java/com/lisb/reader/tts/TtsService.kt` | TTS 前台服务 | ⭐⭐ |
| `app/src/main/java/com/lisb/reader/tts/TtsManager.kt` | TTS 文本拆分与播放 | ⭐⭐ |
| `app/src/main/java/com/lisb/reader/epub/EpubBook.kt` | EPUB 解析 | ⭐⭐ |
| `app/src/main/java/com/lisb/reader/data/SettingsManager.kt` | 设置持久化 | ⭐ |
| `app/src/main/java/com/lisb/reader/ui/BookshelfActivity.kt` | 书架管理 | ⭐ |
| `app/src/main/res/layout/activity_reader.xml` | 阅读器布局 | ⭐ |
| `app/build.gradle.kts` | 构建配置与版本号 | ⭐ |
| `docs/` | 设计文档和开发指南 | 参考 |

### ReaderActivity.kt 结构导航

该文件 ~1300 行，核心区域:

| 行范围 (约) | 内容 |
|-------------|------|
| 1-70 | 类声明、成员变量 |
| 70-170 | onCreate, setupWebView, setupTouchZones |
| 170-370 | 菜单、设置、章节加载 |
| 370-450 | buildHtml (3种模式的HTML生成) |
| 450-520 | buildTtsHtml |
| 520-600 | 分页回调、进度保存 |
| 600-900 | TTS 控制逻辑 |
| 900-1050 | TTS 回调、生命周期 |
| 1050-1300 | **READER_JS** (companion object) |

## 核心架构知识

### 分页引擎 (READER_JS)

**绝对不可触碰的设计约束:**
1. HTML 必须是 `<body><div#lisb-wrapper><div#lisb-content>...</div></div></body>`
2. 滚动机制是 `wrapper.scrollTop = y`（不是 CSS transform）
3. 分页用 `wrapper.clientHeight` 计算（不是 `window.innerHeight`）
4. 行测量时 `scrollTop` 必须为 0
5. 底部遮罩 `#lisb-page-mask` 必须随 scrollTop 同步更新

**分页原理简述:**
```
getClientRects() → lines[{t,b}] → pages[y0,y1,...] → scrollTop=pages[N]
                                                    + mask covers gap
```

### TTS 系统

- `TtsService` 是 Foreground Service + MediaSession
- `TtsManager.splitForTts()` 按句号/问号/感叹号拆分，上限 220 字符
- 高亮通过 `webView.evaluateJavascript("lisbHighlight($i)")` 实现
- 翻页时 TTS 自动暂停（`isTtsActive && ttsService?.playing == true`）

### 三种 HTML 渲染模式

1. **普通模式**: 自定义全套 CSS，适合大多数 EPUB
2. **保留样式模式**: 仅注入 overlay style，保留 EPUB 原始 CSS
3. **TTS 模式**: 纯文本渲染，每句用 `<span class="tts-chunk">` 包裹

## 开发规则

### 必须执行

1. **每次改动必须更新版本号** (`app/build.gradle.kts` 中的 versionCode 和 versionName)
2. **每次改动必须构建验证** (`.\build-release.ps1`)
3. **修改 READER_JS 后必须验证三种 HTML 模式都正确**
4. **修改 CSS 时必须同时更新 preserve/non-preserve/TTS 三处**

### 常见错误避免

| 错误 | 后果 | 正确做法 |
|------|------|----------|
| 用 CSS transform 做滚动 | HarmonyOS 裁剪 bug | 用 wrapper.scrollTop |
| 用 window.innerHeight 做分页 | 设备间可视高度不一致 | 用 wrapper.clientHeight |
| 测量行时不重置 scrollTop | 坐标偏移导致分页错误 | 先 scrollTop=0 再测量 |
| 忘记更新遮罩颜色 | 切换主题后遮罩色不匹配 | mask.bg = body.bg (每次 applyY) |
| TTS 模式缺少 lisb-wrapper | 分页引擎失效 | 三种模式统一 HTML 骨架 |

## 依赖关系

```
epub4j-core:4.2.2  ← EPUB 解析（排除了 slf4j/xmlpull 冲突）
jsoup:1.17.2       ← HTML 内联处理
androidx.media:1.7.0 ← MediaSession + 通知栏控制
kotlinx-coroutines-android:1.7.3 ← 异步操作
```

## 文档索引

- `docs/SESSION_LOG.md` — 开发历史与当前状态
- `docs/AI_DEVELOPMENT_GUIDE.md` — AI 开发规则与规范
- `docs/DESIGN_SPEC.md` — 软件设计规格书
- `docs/REQUIREMENTS_SPEC.md` — 需求规格说明书
- `feature_mobi.md` — Mobi 格式支持设计（未实现）
- `feature_awz3.md` — AZW3 格式支持设计（未实现）
