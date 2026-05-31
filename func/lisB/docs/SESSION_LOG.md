# Session Log — 林中路阅读器 (Holzwege Reader)

> 最后更新: 2026-05-31 | 版本: v1.6.0 (versionCode=10)

## 项目概述

Android EPUB 阅读器应用，包名 `com.lisb.reader`，应用名"林中路"（Holzwege）。
目标平台包括标准 Android 设备和 HarmonyOS 4（Android 兼容层）。

## 当前版本状态 (v1.6.0)

### 已完成功能

| 功能 | 版本 | 状态 |
|------|------|------|
| EPUB 解析与渲染 | v1.0 | ✅ |
| 书架管理（导入/删除/左滑删除） | v1.0-v1.1 | ✅ |
| 虚拟滚动分页引擎（scrollTop架构） | v1.6.0 | ✅ |
| 行级精确分页（getClientRects） | v1.3.0+ | ✅ |
| 主题系统（LIGHT/SEPIA/GREEN/DARK/BLACK） | v1.2 | ✅ |
| 字体大小/行高/字间距调节 | v1.2 | ✅ |
| 字体颜色自定义 | v1.4 | ✅ |
| 目录导航 (TOC) | v1.1 | ✅ |
| 章节内链接跳转 | v1.3 | ✅ |
| 阅读进度保存/恢复 | v1.0 | ✅ |
| TTS 朗读（前台服务 + MediaSession） | v1.3 | ✅ |
| TTS 句级拆分与高亮 | v1.4 | ✅ |
| TTS 翻页时自动暂停 | v1.4 | ✅ |
| TTS 进度保存/恢复 | v1.4 | ✅ |
| TTS 通知栏点击回到APP | v1.6.0 | ✅ |
| 图片点击放大 + 双指缩放 | v1.6.0 | ✅ |
| 页面底部遮罩（防止下一页内容泄漏） | v1.6.0 | ✅ |
| 亮度调节 | v1.2 | ✅ |
| 保留EPUB原始样式选项 | v1.3 | ✅ |
| 自定义应用图标（森林主题） | v1.2 | ✅ |

### 已知问题 / 待优化

- "从当前页开始朗读"的位置映射用字符数比例法，偶有偏差
- EPUB 中包含复杂 CSS（grid/flex 布局）时可能影响行检测精度
- 暂不支持 .mobi / .azw3 格式（有设计文档但未实现）

## 技术架构

### 构建环境

- **语言**: Kotlin 1.9.24
- **AGP**: 8.7.3
- **Gradle**: 8.10.2
- **JDK**: 17
- **SDK**: compileSdk=36, minSdk=24, targetSdk=36
- **构建命令**: `.\build-release.ps1`

### 核心依赖

- `io.documentnode:epub4j-core:4.2.2` — EPUB 解析
- `org.jsoup:jsoup:1.17.2` — HTML 处理
- `androidx.media:media:1.7.0` — MediaSession/通知控制
- AndroidX AppCompat, Material, RecyclerView

### 模块结构

```
com.lisb.reader/
├── LisBApp.kt              — Application 类
├── ui/
│   ├── BookshelfActivity.kt — 书架页面（文件导入、列表管理）
│   ├── ReaderActivity.kt    — 阅读器主页面（核心，~1300行）
│   └── TouchOverlay.kt      — 触摸区域覆盖层
├── epub/
│   └── EpubBook.kt          — EPUB 解析封装
├── tts/
│   ├── TtsService.kt        — 前台朗读服务（MediaSession + 通知）
│   ├── TtsManager.kt        — TextToSpeech 封装（句级拆分）
│   └── TtsActionReceiver.kt — 通知栏按钮广播接收器
└── data/
    └── SettingsManager.kt    — SharedPreferences 设置管理
```

### 分页引擎架构（v1.6.0 — scrollTop 方案）

```
HTML结构:
<body>
  <div id="lisb-wrapper" style="height:100%;overflow:hidden;">
    <div id="lisb-content">...章节内容...</div>
  </div>
</body>

滚动机制: wrapper.scrollTop = y（原生滚动裁剪）
分页计算: wrapper.clientHeight 作为页面可视高度
遮罩: position:fixed 的 #lisb-page-mask 覆盖底部泄漏行
```

**核心 JS 函数:**
- `lisbInit()` — 读取行高，设置内容 padding
- `lisbBuildLines()` — TreeWalker + getClientRects 枚举所有行坐标
- `lisbBuildPages()` — 根据行坐标构建 pages[] 分页数组
- `lisbApplyY(y)` — 设置 scrollTop + 更新底部遮罩
- `lisbScrollByPage(dir)` — 翻页，返回 pageInfo 或 PREV/NEXT_CHAPTER
- `lisbSetupImgZoom()` — 图片点击放大 + 双指缩放

### 关键设计决策

1. **scrollTop vs CSS transform**: v1.6.0 从 transform 架构迁移到 scrollTop。原因是 HarmonyOS WebView 对 transform+overflow:hidden 的裁剪存在偏差。scrollTop 使用浏览器原生滚动裁剪，`clientHeight` 和裁剪边界完全一致。

2. **底部遮罩**: `pages[N+1] - pages[N]` 可能小于 `clientHeight`（最后一行的 top 在视口内但 bottom 超出），造成下一页首行的顶部泄漏到当前页底部。用固定定位的背景色遮罩覆盖这个间隙。

3. **TTS 与翻页互斥**: TTS 播放时触摸翻页会自动暂停 TTS 并弹 Toast 提示。

4. **行检测**: 使用 `Range.getClientRects()` 而非假设等间距行高。这正确处理了图片、标题、不同字号等造成的不规则行间距。

## 版本历史

| 版本 | Code | 主要变更 |
|------|------|----------|
| 1.6.0 | 10 | scrollTop架构重写，底部遮罩，图片双指缩放，通知点击回APP |
| 1.5.1 | 9 | clip-path尝试（过渡版本） |
| 1.5.0 | 8 | line.b 边界检查改进 |
| 1.4.0 | 7 | maxY钳位，TTS翻页互斥，句级高亮 |
| 1.3.0 | 5-6 | 虚拟滚动+getClientRects分页，TOC链接跳转 |
| 1.2.0 | 3-4 | 主题系统，字体设置，亮度调节 |
| 1.1.0 | 2 | 书架管理，左滑删除 |
| 1.0.0 | 1 | 初始版本，基础EPUB阅读 |

## 文件系统说明

- `build-release.ps1` — 一键构建签名 APK（自动生成 keystore）
- `dist/` — 构建输出目录（.apk 文件）
- `keystore.properties` — 签名配置（gitignore）
- `local.properties` — SDK 路径（gitignore）
- `feature_mobi.md` / `feature_awz3.md` — 未来格式支持的设计文档
