# Feature 评估：支持 AZW3 格式

> 状态：**评估（未实现）**  
> 评估日期：2026-05-31  
> 当前 App：林中路阅读器 v1.1.0（仅支持 EPUB / epub4j）  
> 前置依赖：建议与 [feature_mobi.md](feature_mobi.md) 同期实现，可大量复用代码

---

## 1. 格式背景

AZW3 是 Amazon 在 Kindle Fire 时代（2011+）推出的电子书格式，**几乎等同于 KF8-only 的 mobi**：

| 维度 | AZW3 | MOBI (KF8 hybrid) | EPUB3 |
|---|---|---|---|
| 容器 | PalmDB | PalmDB | ZIP |
| 内容 | 完整 KF8（XHTML+CSS） | KF8 + 旧 MOBI6 兼容段 | XHTML+CSS |
| 文件扩展名 | `.azw3` / `.azw` | `.mobi` / `.prc` | `.epub` |
| Magic | `BOOKMOBI` | `BOOKMOBI` | `PK\x03\x04` |
| DRM | 常见（Amazon DRM） | 偶见 | 偶见 (Adobe DRM) |
| KF8 boundary | 通常在 record 0 之后即开始 | 由 EXTH 121 指示中段位置 | 不适用 |

**关键结论**：AZW3 解析复用 MOBI 的 KF8 路径即可。AZW3 不需要实现 MOBI6 LZ77 解压（它本质就是纯 KF8）。

---

## 2. 实现方案

### 推荐方案：复用 MOBI 的 KF8 提取流水线
1. 通过文件扩展名 / Magic 识别为 AZW3
2. 走 [feature_mobi.md](feature_mobi.md) 中 Phase 1 + Phase 2 的代码路径
3. 重组为临时 EPUB，交 `epub4j` 渲染

**前提**：feature_mobi 已实现。如果只做 AZW3 不做 mobi，那么 mobi 的 Phase 1+2 仍需完整实现（无法避免）。

---

## 3. 任务拆解（假设 MOBI 已实现）

### Phase 1 — 格式识别（0.5 d）
- [ ] **T1.1** 文件后缀 `.azw3` / `.azw` 添加到 ACTION_OPEN_DOCUMENT mime filter
- [ ] **T1.2** Magic 字节判定（BOOKMOBI + EXTH cdeType == "EBOK"）
- [ ] **T1.3** 与 mobi 共享同一 `MobiKf8Importer` 入口；按扩展名区分 UI 文案

### Phase 2 — AZW3 特化路径（0.5~1 d）
- [ ] **T2.1** AZW3 中 KF8 boundary 通常缺失或为 0；处理 record 0 即起即解析的情形
- [ ] **T2.2** AZW3 字体 record 可能使用 Amazon obfuscation（EOT-like XOR）；解码后再写入临时 epub
- [ ] **T2.3** AZW3 cover 优先取 EXTH 201/203 record，与 mobi 略有差异

### Phase 3 — DRM 检测（0.5 d）
- [ ] **T3.1** 检测 record 0 中的 DRM 段（offset / count / size 字段非零）
- [ ] **T3.2** 检测 PID/voucher 文件依赖
- [ ] **T3.3** 检测到 DRM → 弹窗 "该 AZW3 受 Amazon DRM 保护，无法打开。请使用 Calibre + DeDRM 插件去除保护后再导入。"

### Phase 4 — UI / 体验（0.5 d）
- [ ] **T4.1** 书架徽章新增 "AZW3"
- [ ] **T4.2** 文件选择器 MIME `application/vnd.amazon.ebook`
- [ ] **T4.3** 导入对话框提示首次解析可能稍慢

### Phase 5 — 测试（1 d）
- [ ] **T5.1** 至少 8 本 AZW3（中文/英文/带图/带嵌入字体/带目录/受 DRM）
- [ ] **T5.2** 与 EPUB 渲染效果对比验证（章节切分、目录、图片）
- [ ] **T5.3** 回归 TTS、翻页、章节跳转、亮度/字体设置
- [ ] **T5.4** 已加密文件的 DRM 提示流程

---

## 4. 工作量估计

| 场景 | 估时 |
|---|---|
| **与 MOBI 一起做**（共享代码路径） | **+2~3 人日**（在 MOBI 6 人日的基础上） |
| **仅做 AZW3**（不做 MOBI） | **5~6 人日**（必须先实现 KF8 提取） |
| **MOBI 已有 → 后补 AZW3** | **2 人日** |

---

## 5. 风险与不确定性

| 风险 | 等级 | 备注 |
|---|---|---|
| Amazon DRM 占比高 | **高** | 用户从 Kindle 库下载的 .azw3 几乎都带 DRM；明确提示是关键 |
| 字体 obfuscation 解码 | 中 | 算法已公开，但样本变种多 |
| Amazon 私有 CSS 扩展（如 `kindle:embed`） | 低 | 大部分浏览器/WebView 会忽略，不影响阅读 |
| 图片资源 record 索引偶有错位 | 低 | 与 mobi 路径共享，一并修复 |
| 部分 AZW3 走完全不同的"KFX"格式（扩展名仍是 .azw） | 中 | KFX 是另一套二进制格式，**不支持**，需 magic 检测后明确报错 |

---

## 6. 建议

- **强烈建议**：与 MOBI 同期开发，共用 KF8 提取代码，总增量仅 2~3 人日。
- 单独开发不划算：底层管道必须重写。
- **必须**给 DRM 文件提供清晰、不诋毁 Amazon 的错误提示和处理建议，避免用户疑惑。
- 不要尝试集成 DeDRM —— 法律风险高，超出阅读器范畴。
- 对 KFX 文件（同扩展名但完全不同格式）做 magic 早期识别并友好降级。

---

## 7. 与 MOBI 的合并建议

如果决定支持 mobi/azw3，建议合并为一个 **"Kindle 格式支持"** Epic：

```
Epic: 支持 Kindle 系列电子书 (MOBI + AZW3)
├─ Story 1: KF8 容器解析 (3 d)             ← 共享
├─ Story 2: KF8 → EPUB 重组 (2 d)          ← 共享
├─ Story 3: 旧 MOBI6 LZ77 兜底 (可选, 3 d)
├─ Story 4: AZW3 字体解混淆 (1 d)
├─ Story 5: DRM 检测与提示 (1 d)           ← 共享
├─ Story 6: 书架/导入 UI 适配 (1 d)        ← 共享
└─ Story 7: 测试与回归 (2 d)               ← 共享
─────────────────────────────────────────
合计：约 10~13 人日（按是否含 MOBI6 兜底）
```
