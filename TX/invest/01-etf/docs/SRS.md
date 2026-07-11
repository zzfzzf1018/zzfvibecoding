# 中国股市 ETF 查询工具 — 软件需求规格说明书（SRS）

| 项目 | 内容 |
| --- | --- |
| 文档名称 | 中国股市 ETF 查询工具 软件需求规格说明书 |
| 文档版本 | v1.0 |
| 创建日期 | 2026-07-10 |
| 上游文档 | PRD v1.0、《数据源调研与选型》 |
| 适用对象 | 开发、测试、架构及后续接手 AI 工程师 |

> 目的：本文档是开发的直接依据。所有功能、接口、数据字段、业务规则与验收标准均在此明确，确保后续 AI/工程师无需回溯产品讨论即可实现。

---

## 1. 引言

### 1.1 目的
定义系统的功能需求、数据需求、接口需求与非功能需求，作为设计、编码、测试与验收的基准。

### 1.2 范围
实现 v1.0：ETF 检索（编号/名称模糊）、基本信息、PE/PB 估值、历史分位数、成分股、历史估值序列。不含自选、提醒（见 PRD 第 9 节）。

### 1.3 约定
- 编号规则：功能需求 `FR-xx`；数据需求 `DR-xx`；接口需求 `IR-xx`；非功能 `NFR-xx`。
- 时间统一使用北京时间（UTC+8）。
- A 股 ETF：上交所/深交所上市、代码 6 位。

---

## 2. 总体描述

### 2.1 产品前景
系统充当"聚合查询层"：向上提供 Web UI 与 API，向下通过**数据源适配层**对接多个外部数据源，中间以**缓存库**保证稳定与性能。

### 2.2 系统架构（建议，分层）
```
[ Web UI ]  ──►  [ API 服务 (FastAPI) ]
                        │
                        ▼
                 [ 业务服务层 ]
                 (检索 / 估值 / 成分股)
                        │
            ┌───────────┴───────────┐
            ▼                       ▼
   [ 数据源适配层 ]          [ 缓存/存储层 ]
   (AkShare / 中证 / 东财)     (SQLite/PostgreSQL)
                        │
                  [ 定时更新任务 ]
```

### 2.3 用户特征
匿名访客为主；无需登录。远期注册用户（不在本期）。

### 2.4 运行环境约束
- 服务端：Linux/Windows，Python ≥ 3.10。
- 建议使用虚拟环境（venv / conda）。
- 需可访问外网以调用数据源（或部署在有外网出口的机器）。

### 2.5 假设与依赖
- 外部数据源（AkShare/中证/东财）在可预见的未来持续可用或可被替代。
- 网络通畅；下游接口变更由适配层吸收。
- 历史估值序列长度满足分位计算最小样本（≥ 250 交易日）。

---

## 3. 功能需求（详细）

### FR-01 ETF 检索
- **描述**：根据关键字返回候选 ETF 列表。
- **输入**：`keyword: string`（1~20 字符）。
- **处理**：
  1. 若 `keyword` 全为数字且长度 ≤ 6：优先按代码前缀/精确匹配。
  2. 否则：对名称、简称、跟踪指数名做**模糊匹配**（含中文子串、拼音首字母，可选）。
  3. 结果按"宽基优先 + 规模降序"排序。
- **输出**：`ETFSearchResult[]`：`{ code, name, type, track_index, track_index_code, latest_price, update_time }`。
- **业务规则**：
  - 空关键字返回空列表（不报错）。
  - 结果上限默认 50 条，支持 `page`/`page_size`。
  - 模糊匹配不区分大小写。
- **异常**：数据源全不可用时返回友好错误，不抛 500。

### FR-02 ETF 基本信息
- **描述**：根据 `code` 返回该 ETF 完整基本信息。
- **输入**：`code: string`（6 位）。
- **输出字段（DR-01）**：见第 4 节 `etf_basic`。
- **业务规则**：代码不存在返回 404 风格提示。

### FR-03 PE/PB 估值
- **描述**：返回当前 PE/PB 及估值日期、来源。
- **输入**：`code`。
- **处理**：
  1. 由 ETF 映射其跟踪指数代码。
  2. 从估值源取该指数最新 PE(TTM)/PB/股息率。
- **输出**：`{ pe_ttm, pb, dividend_yield, valuation_date, source }`。
- **业务规则**：
  - 债券/货币/商品类 ETF 估值不适用 → 返回 `applicable: false` 及原因。
  - 缺失时 `pe_ttm=null`，并标注。

### FR-04 历史分位数（按月序列）
- **描述**：返回最近 N 个月、每月 PE/PB 的历史分位序列（默认近 12 个月），用于展示分位随时间的变化趋势。
- **输入**：`code`, `months`（默认 12，1~36）, `window_years`（默认 5，1~20）。
- **处理**：
  1. 由 ETF 映射其跟踪指数，取该指数全历史 PE/PB 序列。
  2. 对最近 `months` 个自然月，逐月取该月**月末**（当月最后一条有效记录）的 PE/PB 值。
  3. 某月分位 = 月末值在「该月末往前 `window_years` 年」滚动窗口内序列的分位 = (窗口内 ≤ 月末值的样本数)/窗口样本数 × 100%。
  4. 当月无数据 → 该月分位为 `null`。
- **输出**：`{ window_years, months, sample_count, degraded, series: [{ month:"YYYY-MM", pe, pe_percentile, pb, pb_percentile }] }`（series 按月份升序）。
- **业务规则**：
  - 可用历史不足 `window_years` 年 → 用全部历史兜底并置 `degraded=true`。
  - 无任何历史 → 返回空 `series`（`sample_count=0`），前端提示"暂无"。
  - 分位保留 1 位小数。

### FR-05 成分股
- **描述**：返回跟踪指数成分股及权重、行业。
- **输入**：`code`, 可选 `top_n`, `industry_filter`。
- **输出**：`ConstituentStock[]`：`{ stock_code, stock_name, industry(sw_l1), weight, exchange }`。
- **业务规则**：按权重降序；支持行业筛选；支持 Top N。
- **附加**：提供行业分布聚合接口（按 `sw_l1` 汇总权重）。

### FR-06 历史估值序列
- **描述**：返回时间序列 PE/PB，用于绘图与导出。
- **输入**：`code`, `start_date?`, `end_date?`。
- **输出**：`{ date, pe_ttm, pb }[]`（按日期升序）。
- **业务规则**：默认返回全历史；支持 CSV 导出。

### FR-07（远期，本期不实现）
估值对比、自选、提醒——仅在数据模型与接口上预留扩展点。

---

## 4. 数据需求

### DR-01 `etf_basic`（ETF 基本信息表）
| 字段 | 类型 | 说明 |
| --- | --- | --- |
| code | string(6) PK | ETF 代码 |
| name | string | 全称 |
| short_name | string | 简称 |
| type | enum | 宽基/行业/主题/策略/债券/货币/商品/跨境 |
| track_index | string | 跟踪指数名称 |
| track_index_code | string | 跟踪指数代码 |
| fund_manager | string | 基金管理人 |
| custodian | string | 托管人 |
| fund_scale | decimal | 资产规模（亿元） |
| shares | decimal | 份额（亿份） |
| establish_date | date | 成立日期 |
| manager | string | 基金经理 |
| management_fee_rate | decimal | 管理费率(%) |
| custody_fee_rate | decimal | 托管费率(%) |
| tracking_error | decimal | 跟踪误差(%) |
| exchange | enum | SSE/SZSE |
| update_time | datetime | 数据更新时间 |

### DR-02 `index_valuation`（指数估值快照）
| 字段 | 类型 | 说明 |
| --- | --- | --- |
| index_code | string PK | 指数代码 |
| pe_ttm | decimal | 市盈率(TTM) |
| pb | decimal | 市净率 |
| dividend_yield | decimal | 股息率(%) |
| valuation_date | date | 估值日期 |
| source | string | 数据来源标识 |
| update_time | datetime | 更新时间 |

### DR-03 `index_valuation_history`（历史估值序列）
| 字段 | 类型 | 说明 |
| --- | --- | --- |
| index_code | string | 指数代码 |
| date | date | 日期 |
| pe_ttm | decimal | 当日 PE |
| pb | decimal | 当日 PB |
| PK(index_code, date) | | |

### DR-04 `index_constituent`（成分股）
| 字段 | 类型 | 说明 |
| --- | --- | --- |
| index_code | string | 指数代码 |
| stock_code | string | 股票代码 |
| stock_name | string | 股票名称 |
| sw_l1 | string | 申万一级行业 |
| weight | decimal | 权重(%) |
| exchange | enum | 交易所 |
| effective_date | date | 生效日期 |
| PK(index_code, stock_code) | | |

### DR-05 映射关系
- ETF `track_index_code` → `index_valuation` / `index_valuation_history` / `index_constituent` 通过 `index_code` 关联。

---

## 5. 外部接口需求

### IR-01 数据源适配层接口（内部）
定义统一抽象，便于多源替换：
```
interface ValuationSource:
    get_latest(index_code) -> IndexValuation
    get_history(index_code, start, end) -> List[IndexValuationPoint]
interface ConstituentSource:
    get_constituents(index_code) -> List[ConstituentStock]
interface EtfBasicSource:
    list_etfs() -> List[EtfBasic]
    get_etf(code) -> EtfBasic
```
- 实现：AkShare 适配器、中证指数适配器、东方财富适配器。
- 调度策略：主源失败 → 自动切兜底源；结果做一致性校验。

### IR-02 对内 HTTP API（FastAPI 建议）
| 方法 | 路径 | 说明 |
| --- | --- | --- |
| GET | `/api/etf/search?keyword=&page=&page_size=` | FR-01 |
| GET | `/api/etf/{code}/basic` | FR-02 |
| GET | `/api/etf/{code}/valuation` | FR-03 |
| GET | `/api/etf/{code}/percentile?months=12&window_years=5` | FR-04（按月序列） |
| GET | `/api/etf/{code}/constituents?top_n=&industry=` | FR-05 |
| GET | `/api/etf/{code}/valuation/history?start=&end=` | FR-06 |

### IR-03 数据更新接口（定时任务）
- `GET /api/internal/refresh/etf-basic`（每日，增量）
- `GET /api/internal/refresh/valuation`（交易日 T+1）
- `GET /api/internal/refresh/constituents`（按指数公告频率）

### IR-04 UI 接口
- Web 页面：搜索页、详情页（基本信息卡 + 估值卡 + 分位图表 + 成分股表/图 + 历史序列图）。
- 技术建议：前端 React/Vue + ECharts；或通过 Streamlit 快速验证（MVP）。

---

## 6. 非功能需求

### NFR-01 性能
- 命中缓存检索 ≤ 500ms（P95）。
- 详情页聚合（估值+分位+成分股）≤ 2s。
- 历史序列接口支持分页/限长，避免大包。

### NFR-02 可靠性/可用性
- 多数据源冗余；单源故障可降级。
- 对外请求加超时（建议 10s）与重试（≤ 3 次，指数退避）。
- 缓存过期策略：基础信息 24h，估值 T+1，成分股 7d（或公告触发）。
- 数据源连续失败触发告警（日志/通知）。

### NFR-03 数据质量
- 每次写入标注 `source` 与 `update_time`。
- 估值缺失/异常值（PE≤0 或极端值）标记并跳过分位计算。
- 分位计算最小样本阈值 250 交易日。

### NFR-04 安全
- 仅读公开数据；无用户敏感信息（本期）。
- 对外部输入做校验，防注入（参数化查询）。
- API 限流（如 60 次/分钟/IP），防滥用。

### NFR-05 可维护性
- 数据源适配层模块化，新增源只需实现 IR-01 接口。
- 配置化（数据源开关、更新频率在配置文件中）。
- 详细日志与错误码。

### NFR-06 合规
- 页面/接口注明"数据来源"与"仅供研究，不构成投资建议"免责声明。

---

## 7. 数据模型关系
```
etf_basic.code ──(track_index_code)──► index_valuation.index_code
                                    └─► index_valuation_history.index_code
                                    └─► index_constituent.index_code
```
说明：ETF 本身不直接存 PE/PB，估值来自其**跟踪指数**（ETF 是基金的份额，PE/PB 指数是底层指数的估值）。实现时必须先解析 ETF→指数映射。

---

## 8. 验收标准
| 编号 | 标准 | 对应需求 |
| --- | --- | --- |
| AC-01 | 输入"510300"返回含沪深300ETF 的结果 | FR-01 |
| AC-02 | 输入"沪深"返回多个模糊匹配项 | FR-01 |
| AC-03 | `/basic` 返回 DR-01 全部核心字段 | FR-02 |
| AC-04 | `/valuation` 返回 pe/pb/日期/来源；不适用类有 `applicable:false` | FR-03 |
| AC-05 | `/percentile` 返回近 12 个月 `series`，每月含 PE/PB 分位（按月升序） | FR-04 |
| AC-06 | 历史不足 5 年时 `degraded=true`；当月无数据行分位为 null | FR-04/NFR-03 |
| AC-07 | `/constituents` 可排序/行业筛选/Top N，可导出 CSV | FR-05 |
| AC-08 | `/history` 返回按日期升序序列，可 CSV 导出 | FR-06 |
| AC-09 | 关闭主数据源，系统仍可用（降级） | NFR-02 |
| AC-10 | 页面标注数据来源与免责声明 | NFR-06 |

---

## 9. 开发交付建议（给接手 AI）
1. 先实现**数据源适配层 + 缓存库**（IR-01, DR-01~05），保证数据底座稳定。
2. 用 AkShare 快速打通主链路（检索→估值→分位→成分股），可先用 Streamlit 出 MVP。
3. 再补 FastAPI 与 Web 详情页。
4. 加定时更新任务与降级/告警。
5. 单元测试覆盖：分位计算、模糊匹配、源降级。
6. 详细依赖与接口示例见《数据源调研与选型》。
