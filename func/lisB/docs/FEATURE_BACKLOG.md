# 待开发功能列表 — 林中路阅读器

> 按优先级排列，P0 最高

---

## P0 — 核心体验提升

### F-001: 文内搜索

- **描述**: 在当前章节或全书范围内搜索关键词，高亮匹配结果并可跳转
- **技术要点**:
  - WebView 内可用 `window.find()` 或自定义 JS 高亮
  - 全书搜索需遍历 `chapter.plainText`
  - 搜索结果列表需标注章节和上下文
- **预估复杂度**: 中

### F-002: 书签功能

- **描述**: 用户可在任意位置添加书签，从书签列表快速跳转
- **技术要点**:
  - 数据存储: SharedPreferences 或 SQLite（bookId → [{chapter, scrollY, note, timestamp}]）
  - UI: 长按中间区域添加，菜单栏入口查看列表
- **预估复杂度**: 低

### F-003: 高亮与笔记

- **描述**: 长按选择文本后可高亮标注，支持添加笔记
- **技术要点**:
  - WebView 文本选择: 自定义 ActionMode 或 JS selection API
  - 存储高亮范围（章节 + 文本片段 + 偏移量）
  - 渲染时重新应用高亮（在 buildHtml 中注入 `<mark>`）
- **预估复杂度**: 高

---

## P1 — 功能扩展

### F-004: .mobi 格式支持

- **描述**: 支持导入 Kindle .mobi 格式电子书
- **技术要点**: 见 `feature_mobi.md`
- **预估复杂度**: 高

### F-005: .azw3 格式支持

- **描述**: 支持导入 Kindle .azw3 (KF8) 格式
- **技术要点**: 见 `feature_awz3.md`
- **预估复杂度**: 高

### F-006: 阅读统计

- **描述**: 记录阅读时长、已读字数/页数、阅读习惯分析
- **技术要点**:
  - 在 ReaderActivity 中 onResume/onPause 计时
  - 每次翻页累计页数
  - 统计页面展示（简单图表或文字摘要）
- **预估复杂度**: 低

### F-007: 自动翻页

- **描述**: 设定间隔时间自动翻页（配合阅读速度）
- **技术要点**:
  - Handler.postDelayed 循环调用 goNextPage()
  - UI: 设置翻页间隔（秒）
  - TTS 模式下自动禁用
- **预估复杂度**: 低

### F-008: 横屏双栏模式

- **描述**: 横屏时左右分栏显示，类似实体书摊开效果
- **技术要点**:
  - 检测横屏 → CSS columns:2 或两个并列 wrapper
  - 分页逻辑需适配（每"页"实际显示两栏）
  - wrapper.clientHeight 不变，但 clientWidth 减半
- **预估复杂度**: 中

---

## P2 — 锦上添花

### F-009: 自定义字体导入

- **描述**: 用户可导入 .ttf/.otf 字体文件用于阅读
- **技术要点**:
  - 文件存储到 app 内部目录
  - WebView 通过 `@font-face` + `file://` 或 base64 加载
  - 字体选择器 UI
- **预估复杂度**: 中

### F-010: 导出笔记/高亮

- **描述**: 将所有书签、高亮、笔记导出为文本/Markdown 文件
- **技术要点**:
  - 遍历存储数据，格式化输出
  - 使用 Intent.ACTION_CREATE_DOCUMENT 保存
- **预估复杂度**: 低（依赖 F-003）

### F-011: 多书同读（标签页）

- **描述**: 同时打开多本书，通过标签页切换
- **技术要点**:
  - 维护多个 book/chapter/scrollY 状态
  - Tab 栏 UI
  - 内存管理（不能同时加载多个 WebView）
- **预估复杂度**: 中

### F-012: 封面提取与书架美化

- **描述**: 从 EPUB 提取封面图显示在书架，支持网格/列表视图切换
- **技术要点**:
  - epub4j 可读取 cover image
  - 缩略图缓存
  - RecyclerView GridLayoutManager
- **预估复杂度**: 中

### F-013: 朗读语音选择

- **描述**: 让用户选择系统中已安装的 TTS 引擎和语音包
- **技术要点**:
  - `TextToSpeech.getEngines()` 列出可用引擎
  - `tts.voices` 列出语音
  - UI 选择器 + 保存偏好
- **预估复杂度**: 低

### F-014: 夜间模式跟随系统

- **描述**: 根据系统暗色模式自动切换阅读主题
- **技术要点**:
  - `AppCompatDelegate.getDefaultNightMode()` 或 `Configuration.uiMode`
  - 映射: 系统亮色 → LIGHT/SEPIA, 系统暗色 → DARK/BLACK
- **预估复杂度**: 低

### F-015: 翻页动画

- **描述**: 添加翻页过渡动画（滑动、淡入淡出等）
- **技术要点**:
  - 在 lisbApplyY 前后添加 CSS transition
  - 或使用 Android ViewPropertyAnimator 在 WebView 层面
  - 注意不能影响分页精度
- **预估复杂度**: 中

---

## 技术债务

| 项目 | 描述 | 优先级 |
|------|------|--------|
| TD-001 | `ReaderActivity.kt` 过长（~1300行），应拆分 ViewModel | P2 |
| TD-002 | READER_JS 是 Kotlin raw string，调试困难，考虑独立 .js 文件 | P2 |
| TD-003 | 未启用 ProGuard/R8 混淆 | P3 |
| TD-004 | 无单元测试 | P2 |
| TD-005 | TTS `currentPageChunkIndex` 用比例法不够精确 | P1 |
| TD-006 | 未处理超大图片的内存问题（base64 内联） | P2 |
