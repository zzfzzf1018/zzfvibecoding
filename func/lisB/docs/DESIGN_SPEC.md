# 软件设计规格书 — 林中路阅读器 (Holzwege Reader)

> 版本: 1.6.0 | 更新日期: 2026-05-31

## 1. 系统概述

### 1.1 产品定位

轻量级 Android EPUB 阅读器，面向中文用户，注重阅读体验和 TTS 朗读功能。
需兼容 HarmonyOS 4 的 Android 兼容层。

### 1.2 技术选型

| 层次 | 技术 | 理由 |
|------|------|------|
| 语言 | Kotlin | Android 官方推荐 |
| UI框架 | Android View + WebView | WebView 渲染 EPUB HTML 最自然 |
| EPUB解析 | epub4j-core | Maven Central 可用，维护活跃 |
| HTML处理 | jsoup | 内联CSS/图片base64编码 |
| TTS | Android TextToSpeech API | 系统内置，无需额外依赖 |
| 音频焦点 | MediaSession + androidx.media | 标准通知栏控制 |

### 1.3 系统架构图

```
┌──────────────────────────────────────────────────┐
│                 Android System                     │
├──────────────────────────────────────────────────┤
│                                                    │
│  ┌─────────────┐     ┌─────────────────────────┐ │
│  │ Bookshelf   │────▶│ ReaderActivity           │ │
│  │ Activity    │     │  ├─ WebView (EPUB渲染)    │ │
│  └─────────────┘     │  ├─ READER_JS (分页引擎)  │ │
│                       │  ├─ TouchOverlay (手势)   │ │
│                       │  └─ Menu Bars (UI控制)    │ │
│                       └─────────┬───────────────┘ │
│                                 │ bind             │
│                       ┌─────────▼───────────────┐ │
│                       │ TtsService (前台服务)     │ │
│                       │  ├─ TtsManager           │ │
│                       │  ├─ MediaSession         │ │
│                       │  └─ Notification         │ │
│                       └─────────────────────────┘ │
│                                                    │
│  ┌─────────────┐     ┌─────────────────────────┐ │
│  │ EpubBook    │     │ SettingsManager          │ │
│  │ (解析层)     │     │ (SharedPreferences)      │ │
│  └─────────────┘     └─────────────────────────┘ │
└──────────────────────────────────────────────────┘
```

## 2. 核心模块设计

### 2.1 分页引擎 (READER_JS)

#### 2.1.1 设计目标

- 精确到行的分页，不允许行被截断
- 兼容各种 EPUB CSS（不同行高、图片、标题）
- 在 HarmonyOS WebView 上正确渲染

#### 2.1.2 架构演进

| 版本 | 方案 | 问题 |
|------|------|------|
| v1.0-v1.2 | 固定行高步进 | 行高不一致时截断 |
| v1.3-v1.5 | CSS transform + overflow:hidden | HarmonyOS 裁剪偏差 |
| v1.6.0 | scrollTop + wrapper.clientHeight | ✅ 当前方案 |

#### 2.1.3 当前方案详细设计

**HTML 结构:**
```html
<body style="overflow:hidden; height:100%; margin:0; padding:0;">
  <div id="lisb-wrapper" style="height:100%; overflow:hidden; padding:0 20px; box-sizing:border-box;">
    <div id="lisb-content" style="paddingTop/Bottom: lh*0.5">
      <!-- 章节 HTML 内容 -->
    </div>
  </div>
</body>
```

**分页流程:**
```
window.onload
  └─▶ lisbInitAll()
       ├─▶ lisbInit()          // 读取 computed line-height
       ├─▶ lisbBuildLines()    // 枚举行坐标 [{t, b}, ...]
       ├─▶ lisbBuildPages()    // 生成 pages[] = [0, y1, y2, ..., maxY]
       └─▶ lisbSetupImgZoom()  // 绑定图片点击事件

翻页:
  lisbScrollByPage(+1/-1)
    └─▶ lisbApplyY(pages[newIdx])
         ├─▶ wrapper.scrollTop = y
         └─▶ 更新 #lisb-page-mask 高度和颜色
```

**行检测算法 (`lisbBuildLines`):**
1. 将 wrapper.scrollTop 设为 0（确保坐标从顶部算起）
2. 使用 TreeWalker 遍历所有文本节点
3. 对每个文本节点创建 Range，调用 getClientRects()
4. 收集所有 rect 的 {top, bottom}
5. 同样收集所有 `<img>` 的 getBoundingClientRect()
6. 按 top 排序，合并相邻（Δt ≤ 2px）的 rect
7. 恢复 scrollTop

**分页算法 (`lisbBuildPages`):**
```
vh = wrapper.clientHeight  // 精确可视高度
pages = [0]
py = 0
while:
  找到第一个 lines[i] 满足 lines[i].b > py + vh
  next = lines[i].t  // 下一页从这行顶部开始
  pages.push(next)
  py = next
确保 maxY 可达
```

**底部遮罩机制 (`lisbApplyY`):**
```
gap = wrapper.clientHeight - (pages[idx+1] - y)
if gap > 0:
  显示 #lisb-page-mask, height = gap px
  backgroundColor = body 背景色
else:
  隐藏 mask
```

### 2.2 TTS 朗读系统

#### 2.2.1 组件关系

```
ReaderActivity
  │ toggleTts() / promptStartPosition()
  │
  ├─▶ TtsService (Foreground Service)
  │    ├─ MediaSession (系统媒体控制)
  │    ├─ Notification (通知栏: 上一章/暂停/下一章/停止)
  │    └─ TtsManager
  │         ├─ splitForTts(text, maxLen=220)  // 句级拆分
  │         ├─ speak(text, prefix, startIdx)
  │         └─ callbacks: onChunkStart, onAllFinished
  │
  └─▶ WebView (TTS HTML 模式)
       ├─ <span class="tts-chunk"> 包裹每个句子
       ├─ lisbHighlight(i) 高亮当前句
       └─ 自动翻页到高亮句所在页
```

#### 2.2.2 状态机

```
[未激活] ──toggleTts()──▶ [语速设置对话框]
                              │
                    确定 ──▶ [选择起始位置]
                              │
              ┌───────────────┼───────────────┐
              ▼               ▼               ▼
        [从上次位置]    [从当前页]      [从头开始]
              │               │               │
              └───────┬───────┘───────────────┘
                      ▼
              [TTS 播放中] ◀──── 恢复
                 │    │
        暂停 ────┘    └──── 翻页(自动暂停)
                 │
                 ▼
            [TTS 暂停] ──── 3选项对话框:
                              ├─ 继续播放
                              ├─ 从当前页重新开始
                              └─ 停止
```

### 2.3 EPUB 解析 (EpubBook)

- 使用 epub4j 解析 spine 获取章节顺序
- jsoup 处理 HTML: 内联 CSS、将图片转为 base64 data URI
- 提取 plainText（用于 TTS 和进度估算）
- Chapter 数据类: `{index, title, headHtml, bodyHtml, plainText, sourceHref}`

### 2.4 设置管理 (SettingsManager)

基于 SharedPreferences，管理：
- 主题（5种）、字体、字号、行高、字间距、字体颜色
- 亮度设置
- TTS 语速
- 阅读进度: `saveProgress(bookId, chapter, scrollY)`
- TTS 进度: `saveTtsProgress(bookId, chapter, chunkIndex)`
- preserveEpubStyle 开关

## 3. UI 设计

### 3.1 布局结构 (activity_reader.xml)

```
FrameLayout (root, fitsSystemWindows)
├── LinearLayout (vertical, 全屏)
│   ├── headerIndicator (章节标题, 12sp)
│   ├── WebView (weight=1, 主内容区)
│   └── footerIndicator (页码, 12sp)
├── topBar (浮动, 返回按钮+书名)
└── bottomBar (浮动, 进度条+功能按钮)
```

### 3.2 触摸区域

```
┌──────────────────────┐
│      顶部 20%         │ → 切换菜单
├──────┬────────┬──────┤
│      │        │      │
│ 左   │  中间  │  右  │
│ 33%  │        │ 67%  │ → 左=上一页, 右=下一页, 中=菜单
│      │        │      │
├──────┴────────┴──────┤
│      底部 (无特殊区)  │
└──────────────────────┘

水平滑动: 左滑=下一页, 右滑=上一页
```

## 4. 数据流

### 4.1 章节加载流程

```
loadChapter(chapterIdx, scrollY)
  ├─ book.chapters[idx]
  ├─ buildHtml(chapter) 或 buildTtsHtml(chapter)
  ├─ webView.loadDataWithBaseURL(html)
  ├─ onPageFinished:
  │    ├─ lisbSetY(scrollY)        // 恢复滚动位置
  │    └─ refreshPageInfo()        // 更新页码显示
  └─ saveProgress()
```

### 4.2 进度保存格式

```
SharedPreferences key: "progress_${bookId}"
Value: "$chapter|$scrollY"

TTS key: "tts_progress_${bookId}"
Value: "$chapter|$chunkIndex"
```
