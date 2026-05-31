# Code Review SKILL — 林中路阅读器

> 开发完成后调用此 SKILL 进行代码审查。
> 按检查清单逐项验证，输出 PASS/FAIL + 具体说明。

---

## 使用方式

开发完成后，对 AI 说：
```
请按照 docs/SKILL_CODE_REVIEW.md 对本次修改进行 Code Review
```

---

## 审查清单

### 1. 版本号 [必须]

- [ ] `app/build.gradle.kts` 中 `versionCode` 已递增
- [ ] `versionName` 已按 SemVer 更新
- [ ] 版本号与改动规模匹配（bug fix=PATCH, 新功能=MINOR, 架构变更=MAJOR）

**检查方法:**
```
读取 app/build.gradle.kts 中 versionCode 和 versionName
对比上次已知版本（见 docs/SESSION_LOG.md）
```

---

### 2. 构建验证 [必须]

- [ ] `.\build-release.ps1` 或 `.\gradlew.bat :app:assembleRelease` 执行成功
- [ ] 无 Kotlin 编译错误
- [ ] 无严重 warning（unused parameter 等轻微 warning 可忽略）

**检查方法:**
```powershell
.\gradlew.bat :app:assembleRelease --no-daemon 2>&1 | Select-String "error|e: |FAIL"
```

---

### 3. 分页引擎完整性 [如涉及 READER_JS]

- [ ] HTML 骨架保持 `<body><div#lisb-wrapper><div#lisb-content>...</div></div></body>`
- [ ] 三种 HTML 模式（普通/保留样式/TTS）都已同步更新
- [ ] `lisbBuildLines()` 中测量前设置 `scrollTop=0`，测量后恢复
- [ ] `lisbBuildPages()` 使用 `wrapper.clientHeight` 作为 vh（不是 `window.innerHeight`）
- [ ] `lisbApplyY()` 同时更新 scrollTop 和 #lisb-page-mask
- [ ] `lisbMaxY()` 使用 `wrapper.scrollHeight - wrapper.clientHeight`
- [ ] 没有使用 CSS `transform: translateY` 做页面滚动
- [ ] 没有使用 `clip-path` 做页面裁剪

**检查方法:**
```
grep "translateY" ReaderActivity.kt  → 应只出现在注释或 img-zoom 中
grep "clipPath\|clip-path" ReaderActivity.kt → 应无匹配
grep "window.innerHeight" ReaderActivity.kt → 应不在 lisbBuildPages/lisbMaxY 中
grep "lisb-wrapper" ReaderActivity.kt → 应出现在所有 buildHtml/buildTtsHtml 中
```

---

### 4. TTS 系统完整性 [如涉及 TTS]

- [ ] `TtsService` 保持 Foreground Service 状态（startForeground 未被移除）
- [ ] 通知栏包含 contentIntent（点击回到 APP）
- [ ] `TtsManager.splitForTts()` 拆分逻辑未被破坏
- [ ] TTS 模式 HTML 包含 `lisb-wrapper` 和正确的 CSS
- [ ] `lisbHighlight(i)` 函数存在且能触发自动翻页
- [ ] `__lisbMinChunk` 机制保留（防止旧回调干扰）

---

### 5. 主题兼容性 [如涉及 CSS/颜色]

- [ ] 5种主题下文字/背景颜色正确
- [ ] `#lisb-page-mask` 背景色从 `getComputedStyle(document.body).backgroundColor` 获取
- [ ] 图片放大覆盖层背景色固定为 `rgba(0,0,0,0.92)`（不受主题影响）
- [ ] header/footer indicator 颜色适配当前主题

---

### 6. 安全性检查

- [ ] 无硬编码密钥/密码（keystore.properties 在 .gitignore 中）
- [ ] WebView 不加载外部 URL（`shouldOverrideUrlLoading` 拦截所有链接）
- [ ] 无不必要的网络权限
- [ ] 用户输入（文件名等）有适当校验

---

### 7. 性能检查

- [ ] 无无限循环风险（`lisbBuildPages` 有 guard 计数器）
- [ ] 大章节（>10000行）不会导致 ANR（JS 执行在 WebView 线程）
- [ ] 没有在主线程做文件 I/O（EPUB 解析应在协程/后台）
- [ ] 图片未以全分辨率 base64 内联（应有 maxWidth 约束）

---

### 8. 代码质量

- [ ] 无重复代码（相同逻辑不在多处重复）
- [ ] 变量命名清晰（`currentVirtualY` 而非 `y`）
- [ ] 关键逻辑有注释说明
- [ ] 无遗留的调试代码（console.log, Log.d 等）
- [ ] Kotlin 无 unused imports（IDE 应自动移除）

---

### 9. 向后兼容

- [ ] 已保存的阅读进度格式未改变（`"$chapter|$scrollY"`）
- [ ] 已保存的 TTS 进度格式未改变（`"$chapter|$chunkIndex"`）
- [ ] SharedPreferences key 名未改变
- [ ] 如进度格式必须改变，有迁移逻辑

---

### 10. 文档同步 [建议]

- [ ] `docs/SESSION_LOG.md` 版本历史已更新
- [ ] `docs/FEATURE_BACKLOG.md` 已完成的功能已标记
- [ ] `docs/REQUIREMENTS_SPEC.md` 需求变更记录已更新

---

## 输出格式

审查完成后，按以下格式输出结果：

```markdown
## Code Review 结果

**版本**: v{x.y.z} (code={N})
**审查日期**: {date}
**涉及文件**: {file list}

### 检查结果

| # | 检查项 | 结果 | 备注 |
|---|--------|------|------|
| 1 | 版本号 | ✅ PASS | versionCode=N, versionName="x.y.z" |
| 2 | 构建验证 | ✅ PASS | BUILD SUCCESSFUL |
| 3 | 分页引擎 | ⬜ N/A | 本次未修改 |
| ... | ... | ... | ... |

### 发现的问题

- **[严重]** 描述...
- **[建议]** 描述...

### 总结

✅ 通过 / ⚠️ 有建议 / ❌ 需修复
```

---

## 严重级别定义

| 级别 | 含义 | 是否阻断 |
|------|------|----------|
| 🔴 严重 | 会导致崩溃、数据丢失或功能失效 | 必须修复后才能发布 |
| 🟡 警告 | 可能在部分设备上出问题 | 建议修复 |
| 🔵 建议 | 代码质量或可维护性问题 | 可选修复 |
| ⬜ N/A | 本次修改未涉及此项 | 跳过 |
