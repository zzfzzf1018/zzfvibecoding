# AI 开发指南 — 林中路阅读器

> 本文件用于指导 AI 助手对本项目进行开发和维护。

## 强制规则

### 1. 版本号管理（必须）

**每次修改代码后、构建前，必须更新版本号：**

- 文件: `app/build.gradle.kts`
- 位置: `defaultConfig` 块内
- 规则:
  - `versionCode`: 递增 +1（整数）
  - `versionName`: 遵循 SemVer（MAJOR.MINOR.PATCH）
    - PATCH: bug 修复、小调整
    - MINOR: 新功能、非破坏性改动
    - MAJOR: 架构重写、不兼容变更

```kotlin
defaultConfig {
    versionCode = N+1
    versionName = "x.y.z"
}
```

### 2. 构建验证（必须）

每次代码修改后必须构建验证：

```powershell
.\build-release.ps1
```

或最小验证：
```powershell
.\gradlew.bat :app:assembleRelease --no-daemon
```

确认 `BUILD SUCCESSFUL` 后才能交付。

### 3. 不可破坏的核心机制

修改以下模块时需特别谨慎：

| 模块 | 风险 | 注意事项 |
|------|------|----------|
| READER_JS (分页引擎) | 高 | 涉及行检测、分页、遮罩的协同工作 |
| lisbBuildLines/lisbBuildPages | 高 | getClientRects 坐标系依赖 scrollTop=0 |
| lisbApplyY | 高 | scrollTop + 遮罩必须同步更新 |
| TtsService 前台服务 | 中 | 必须维持 foreground notification |
| HTML 结构 (lisb-wrapper) | 高 | wrapper/content 嵌套结构不可改变 |

### 4. 测试要点

修改后需重点验证：
- [ ] 翻页是否平滑，底部无截断行
- [ ] 最后一页是否能正确显示
- [ ] 章节切换是否正常
- [ ] TTS 开始/暂停/恢复是否正常
- [ ] 主题切换后颜色是否正确（包括遮罩颜色）
- [ ] 图片点击放大和缩放

## 开发规范

### 代码风格

- Kotlin: 标准 Android Kotlin 风格
- JS (READER_JS): 紧凑单行风格（在 Kotlin raw string 中），变量名以 `lisb` 前缀
- 缩进: Kotlin 4空格, JS 在字符串中用 2空格

### HTML 三种模式

1. **普通模式** (`buildHtml`, non-preserve): 自定义 CSS，适合大多数 EPUB
2. **保留样式模式** (`buildHtml`, preserve): 注入 overlay style，保留 EPUB 原始 CSS
3. **TTS 模式** (`buildTtsHtml`): 纯文本 + span 高亮

三种模式都必须使用相同的 HTML 骨架：
```html
<body>
  <div id="lisb-wrapper">
    <div id="lisb-content">...</div>
  </div>
</body>
```

### 分页引擎原理

```
1. lisbInit(): 读取 line-height，设置 content padding
2. lisbBuildLines(): scrollTop=0 时用 getClientRects 测量所有行
3. lisbBuildPages(): 用 wrapper.clientHeight 作为 vh，逐行检查 line.b > py + vh
4. lisbApplyY(y): 设置 wrapper.scrollTop = y，计算并显示底部遮罩
5. lisbScrollByPage(dir): 在 pages[] 中步进
```

### 添加新功能的检查清单

1. 确认不影响分页引擎（READER_JS）
2. 确认三种 HTML 模式都已更新（如涉及 HTML/CSS）
3. 更新版本号
4. 构建验证
5. 更新 docs/SESSION_LOG.md 中的功能表

## 常见陷阱

### 分页相关

- **不要用 CSS transform 做滚动** — HarmonyOS WebView 裁剪有 bug
- **不要用 window.innerHeight 做分页** — 在部分设备上与实际可视区域不符
- **必须用 wrapper.clientHeight** — 它等于实际裁剪边界
- **getClientRects 测量时 scrollTop 必须为 0** — 否则坐标会偏移
- **Math.ceil(rc.bottom)** — 防止亚像素舍入导致行底超出

### TTS 相关

- TTS chunk 拆分在 `TtsManager.splitForTts()` 中完成（220字符上限）
- `__lisbMinChunk` 防止旧回调干扰新播放位置
- 章节切换时必须停止当前 TTS

### 构建相关

- JDK 必须是 17（不是 11，不是 21）
- 首次构建会自动生成 keystore（保存在 `build/keystores/`）
- `keystore.properties` 和 `local.properties` 在 .gitignore 中
