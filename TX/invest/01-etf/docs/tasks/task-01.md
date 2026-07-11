# task-01：历史分位改为「按月 · 最近12个月」序列

- 创建人：AI（CodeBuddy）
- 日期：2026-07-11
- 关联需求：PRD F-04 / SRS FR-04（历史分位数）——本次为行为变更
- 状态：已完成（采用方案 B · 滚动近 5 年窗口，见下）
- 关联记录：CHG-20260711-01

## 1. 目标与范围
### 目标
将"历史分位"从**单点分位**（当前值在某窗口的一个分位）改为**按月的时间序列**：一次返回并展示**最近 12 个月**、每月一个 PE 分位与 PB 分位，便于观察估值分位随时间的变化趋势。

### 范围
- 后端：新增"按月分位序列"计算逻辑、API 输出、schema。
- 前端：`index.html` 历史分位区块改为展示 12 个月的表格/迷你趋势。
- 文档：同步更新 PRD F-04 / SRS FR-04 描述与验收；追加 `CHG-` 记录。

### 非目标
- 不改动历史估值采集/落库逻辑（沿用 `index_valuation_history`）。
- 不引入前端图表库（用轻量表格 + 文本；如需 ECharts 另开 task）。

## 2. 方案设计（含待确认项）
### 2.1 分位语义（已定：方案 B）
> 结论：从普通用户视角，采用**方案 B（滚动近 5 年窗口）**——与主流基金 App 口径一致、12 个月之间可横向比较、早期数据少时更稳。窗口默认 **5 年**（覆盖一轮牛熊、多数 ETF 数据够）。

「某月的历史分位」两种常见口径：

- **方案 A（推荐）· 截至当月的历史分位**：
  取该月**月末**（该月最后一个有数据交易日）的 PE 值，计算它在**从有数据起 → 该月末**的全部历史序列中的分位。
  含义：站在每个月末回看，"当时估值处于历史什么位置"。更贴近"历史分位"本义，趋势可解释性强。

- **方案 B · 固定窗口滚动分位**：
  对每个月末值，只在其**过去 N 年**（如 5 年）滚动窗口内计算分位。
  含义：始终用近 N 年为基准，消除"早期样本少"的影响，但实现与解释更复杂。

> 默认按 **方案 A** 实现（复用现有 `_percentile_at(values, target)` 辅助）。如需方案 B 请告知。

### 2.2 计算流程（方案 A）
1. `get_history(index_code)` 取全历史 PE/PB 序列（升序）。
2. 确定最近 12 个自然月（含当月）。对每个月：
   - 取该月内最后一条有效记录作为"月末值"（PE、PB 各自处理缺失）。
   - 该月无数据 → 该月分位为 `null`（前端显示"-"）。
   - PE 月末分位 = 月末 PE 在"起始 → 该月末"PE 子序列中的分位；PB 同理。
3. 汇总为 12 条 `{month, pe_value, pe_percentile, pb_value, pb_percentile}`（按月份升序）。
4. 样本不足（总样本 < `min_samples`）沿用降级提示 `degraded/note`。

### 2.3 分层与文件（遵循 api→services→repo 松耦合）
- `src/app/models/schemas.py`：新增 `MonthlyPercentilePoint`、`MonthlyPercentileView`（不删旧 `PercentileView`，避免破坏其它引用）。
- `src/app/services/percentile_service.py`：新增 `get_monthly_percentile(index_code, months=12)`；复用 `_percentile_at`。不 import akshare/不发请求。
- `src/app/api/router_valuation.py`：`/{code}/percentile` 增加"按月"输出（见 2.4 契约）。
- `src/app/templates/index.html`：历史分位区块渲染 12 个月序列。

### 2.4 接口契约（IR-02）
- 复用路径 `GET /api/etf/{code}/percentile`，改为返回按月序列：
```json
{
  "months": 12,
  "sample_count": 1234,
  "degraded": false,
  "series": [
    {"month": "2025-08", "pe": 12.3, "pe_percentile": 18.5, "pb": 1.4, "pb_percentile": 22.1},
    {"month": "2025-09", "pe": null, "pe_percentile": null, "pb": null, "pb_percentile": null}
  ]
}
```
- 兼容策略：保留 `window` 查询参数但忽略（或用于将来）；无历史时返回 `{"applicable": false, "reason": ...}`（沿用现状）。

## 3. 实施步骤（拆解）
- [ ] schemas 新增 `MonthlyPercentilePoint` / `MonthlyPercentileView`
- [ ] service 新增 `get_monthly_percentile`（按月分组 + 逐月分位）
- [ ] router 输出改为按月序列（保留无数据的 applicable=false 分支）
- [ ] 前端 `index.html` 历史分位区块改为 12 个月表格
- [ ] 更新 PRD F-04 / SRS FR-04 描述与验收标准
- [ ] tests：新增按月分位单元测试（构造多月序列，校验分位与缺月为 null）
- [ ] 追加 `docs/开发记录.md` 的 `CHG-` 记录，回填本 task 状态

## 4. 测试计划
- 单元：构造跨 ≥13 个月的 PE/PB 历史，断言：
  - 返回恰好 12 个月、按月升序；
  - 某月末分位 = 手工计算值；
  - 中间缺数据的月 → 分位为 `null`；
  - 总样本 < min_samples 时 `degraded=true`。
- 回归：`pytest` 全量通过（不破坏搜索/估值/成分股用例）。
- 手动：本地启动，详情页历史分位区块显示 12 个月。

## 5. 风险与回滚
- 风险：历史数据不足 12 个月时，前面月份多为 `null`（属预期，前端显示"-"）。
- 风险：API 输出结构变化 → 前端需同步（本次一并改）；若有外部调用方依赖旧结构，需评估（当前仅本项目前端消费）。
- 回滚：保留旧 `PercentileView` 与旧方法，回退只需还原 router 输出与前端渲染。
