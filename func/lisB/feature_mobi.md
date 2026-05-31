# Feature 评估：支持 MOBI 格式

> 状态：**评估（未实现）**  
> 评估日期：2026-05-31  
> 当前 App：林中路阅读器 v1.1.0（仅支持 EPUB / epub4j）

---

## 1. 格式背景

MOBI 是 Amazon Kindle 早期使用的电子书格式，源自 Mobipocket / PalmDOC。常见两种内部变体：

| 变体 | 标识 | 内部结构 |
|---|---|---|
| PalmDOC / MOBI6（旧） | `BOOKMOBI` | LZ77 (PalmDOC) 压缩的 HTML（HTML 3.2 子集） + 自定义索引 |
| KF8 / MOBI8（新，混合） | `BOOKMOBI` + 第二个 EXTH 段 | 内含一份完整 EPUB3（XHTML + CSS + 字体） |

绝大多数当代 mobi 文件是 **KF8 混合**，里面同时打包了旧 mobi（向后兼容）和新的 EPUB3 内容。

---

## 2. 实现方案对比

### 方案 A：内置纯 Kotlin/Java MOBI 解析器
- 自己解析 PalmDB 头、PalmDOC/HUFF-CDIC 解压、Record 索引、EXTH 元数据、KF8 段定位。
- 不引入运行时依赖，APK 不变大；但工作量大且需要兼容旧 mobi 的私有索引格式。

### 方案 B：用 KF8 段内嵌的 EPUB（推荐）
- 仅解析 PalmDB 容器找到 KF8 boundary，把后半段当 EPUB 写入临时文件，复用现有 `epub4j` 流程。
- **覆盖 95%+ 的现代 mobi**，旧 MOBI6-only 文件不支持（少见，可降级提示用户）。
- 实现成本远低于方案 A。

### 方案 C：调用 Calibre/KindleUnpack 命令行
- Android 上不可行（无 Python/perl 环境，APK 体积爆炸）。**排除**。

### 方案 D：使用现有开源 Java 库
- `mobi-java`（github）—— 不活跃、依赖老
- `epublib` 旧分支有实验性 mobi —— 已废弃
- **结论**：没有可直接信任的成熟库；自行最小实现优于引入半死库。

---

## 3. 任务拆解（推荐方案 B + 方案 A 兜底）

### Phase 1 — 解析与识别（1.5 d）
- [ ] **T1.1** 编写 `MobiContainer` 解析 PalmDB header（78 B）+ record offsets
- [ ] **T1.2** 解析第 0 个 record（PalmDOC header + MOBI header + EXTH）：取得编码、title、author、KF8 boundary
- [ ] **T1.3** 写单元测试：3~5 本不同来源的 mobi 文件覆盖 (KF8 / 仅 MOBI6 / 带 DRM)

### Phase 2 — KF8 提取路径（方案 B 主路径，2 d）
- [ ] **T2.1** 从 KF8 boundary 开始重组 record 流：解析 KF8 自己的 PalmDOC + INDX
- [ ] **T2.2** 抽取 `RESC`、`FDST`、`SKEL`、`FRAG` 段，按 SKEL 索引还原 XHTML 文件
- [ ] **T2.3** 抽取 `RESC`/`FONT`/`IMG` 资源，重组 OPF + container.xml，打包为临时 .epub
- [ ] **T2.4** 调通 `EpubBook.open()` 直接读取重组 epub
- [ ] **T2.5** Bookshelf import flow 支持 `.mobi` 后缀；提示 "导入中…" 进度

### Phase 3 — MOBI6 解压兜底（方案 A 简化版，2~3 d）
适用于 KF8 boundary 缺失的纯旧 mobi。
- [ ] **T3.1** 实现 PalmDOC LZ77 解压（约 80 行）
- [ ] **T3.2** 实现 HUFF/CDIC 解压（少数 mobi 使用，约 150 行；可后置）
- [ ] **T3.3** 合并所有 text record，按 `<mbp:pagebreak>` 切章节
- [ ] **T3.4** 抽取 IMG record，重写 `<img recindex="N">` 为 data: URI
- [ ] **T3.5** 输出与 EpubBook 兼容的 chapter 列表（不经过 epub4j）

### Phase 4 — DRM 检测（0.5 d）
- [ ] **T4.1** 解析 EXTH record 209 (TamperProofKeys) 与 records 末尾的 DRM 标记
- [ ] **T4.2** 检测到 DRM 时给出明确提示："此文件已加密，无法打开"

### Phase 5 — UI 与体验（1 d）
- [ ] **T5.1** 文件选择器 MIME 添加 `application/x-mobipocket-ebook`
- [ ] **T5.2** 书架 UI 显示格式徽章 (EPUB/MOBI)
- [ ] **T5.3** 导入失败 toast 区分原因（DRM / 旧版未支持 / 文件损坏）
- [ ] **T5.4** 进度条对话框（mobi 解析较慢）

### Phase 6 — 测试与回归（1 d）
- [ ] **T6.1** 至少 10 本 mobi（中文/英文/带图/带 footnote/纯旧/KF8）真机测试
- [ ] **T6.2** Bookshelf 导入/删除/重复检测
- [ ] **T6.3** TTS、翻页、章节跳转回归
- [ ] **T6.4** APK 体积与冷启动时间对比

---

## 4. 工作量估计

| 阶段 | 估时 |
|---|---|
| Phase 1 解析识别 | 1.5 人日 |
| Phase 2 KF8 主路径 | 2 人日 |
| Phase 3 MOBI6 兜底（可裁剪） | 2~3 人日 |
| Phase 4 DRM 检测 | 0.5 人日 |
| Phase 5 UI/体验 | 1 人日 |
| Phase 6 测试回归 | 1 人日 |
| **合计（含 MOBI6 兜底）** | **8~9 人日** |
| **合计（仅 KF8 主路径）** | **6 人日** |

---

## 5. 风险与不确定性

| 风险 | 等级 | 备注 |
|---|---|---|
| PalmDOC/HUFF 解压细节边界条件多 | 中 | 参考 KindleUnpack/calibre 源码逐字段对照 |
| KF8 SKEL 索引结构无官方文档 | 中 | 仅可参考逆向工程文档，需大量样本验证 |
| 加密 mobi 占比未知 | 低 | 检测即可，无需破解 |
| 中文编码 (cp1252 / utf-8 / gbk) 混杂 | 低 | EXTH 96 + MOBI header codepage 字段可判定 |
| 字体嵌入资源体积大 | 低 | 按需提取，与 EPUB 流程一致 |

---

## 6. 建议

- **MVP 一期**：仅做 Phase 1 + 2 + 4 + 5 + 6（KF8 主路径 + DRM 提示），约 **6 人日**，可覆盖 95% 现代 mobi。
- **后续二期**：补 Phase 3（MOBI6 兜底），追加 2~3 人日，覆盖剩余历史文件。
- 不建议引入 native (C/C++) MOBI 库 — 维护成本和 NDK 复杂度不划算。
