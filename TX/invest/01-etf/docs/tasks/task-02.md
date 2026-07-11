# task-02：让 ETF 详情页实际显示数据（基本信息 / 估值 / 历史分位 / 成分股）

- 创建人：AI（CodeBuddy）
- 日期：2026-07-11
- 关联需求：用户新需求（查询后选择 ETF 需显示基本信息含发行商/费率/规模、估值 PE/PB 实际值、PB/PE 历史分位数、成分股列表；目前只是空模板）
- 关联 BUG：docs/bugs/bug-001.md
- 关联记录：CHG-20260711-02
- 关联深层 BUG：docs/bugs/bug-002.md（同进程污染 + 解析 bug）
- 状态：已完成

## 1. 目标与范围
- 目标：详情面板四个区块**真实出数**，而非空模板。
- 范围：修复数据源码（akshare 接口已过期）、补齐"按需拉取并落库"链路（成分股、历史分位）、修正东财脏数据。
- 非目标：不重写前端框架；不引入新数据库；不做历史分位算法的口径变更（沿用 task-01 的滚动 5 年窗口）。

## 2. 方案设计
### 2.1 数据源现状（已联网探测，akshare 1.18.64）
| 用途 | 旧接口（失效） | 新接口（可用） | 取数键 |
|---|---|---|---|
| ETF 基本信息 | `fund_etf_info_em`(删) | `fund_overview_em(code)` ✅ | 代码 |
| 指数当前 PE/PB | `stock_index_value_name_em`(删) | `stock_zh_index_value_csindex(code)` ✅（权威/TTM） | 代码 |
| 指数 PE/PB 历史 | 无 | `stock_index_pe_lg(name)`/`stock_index_pb_lg(name)` ✅（主流宽基） | 指数名(去"指数") |
| 成分股 | — | `index_stock_cons_csindex(code)` + `index_stock_cons_weight_csindex(code)` ✅ | 代码 |
| 成分股行业 | `stock_individual_info_em`(逐股) | 保留（top20 可接受） | 代码 |

### 2.2 关键决策
- **名→码解析**：`stock_zh_index_value_csindex`/`index_stock_cons_csindex` 强制按**代码**；
  `fund_overview_em` 只给指数**名**。新增 `_resolve_index_code(name)`：内置主流指数映射 +
  运行时 `index_code_id_map_em`（可用时）增强；命中即写入 `track_index_code` 并落库。
- **估值当前值**：优先 `stock_zh_index_value_csindex(code)`（TTM）；无代码时回退 `stock_index_pe_lg(name)` 末行（静态）。
- **历史分位**：`PercentileService` 注入 `valuation_sources`，历史不足时按需拉取 `stock_index_pe_lg/pb_lg`（名）全量并按窗口落库，再计算 12 个月分位。
- **成分股**：`ConstituentService` 注入 `constituent_sources`，本地库空时按需拉取 + `replace_constituents` 落库。
- **东财脏数据**：`EmValuationSource` 增加 0<PE/PB<1000 合理性校验，越界返回 `None`（走降级），杜绝 `pe=5.5e13` 污染缓存。
- **中证兜底 404**：保留（记录日志、返回 None），不作为主路径；不猜测其 URL。

### 2.3 涉及模块/文件
- src/app/datasource/akshare_src.py（重写 basic/valuation/constituent + 新增解析助手）
- src/app/datasource/interfaces.py（`ValuationSource` 协议加 `index_name` 可选参）
- src/app/datasource/csindex_src.py / em_src.py（同步签名 + 东财校验）
- src/app/services/valuation_service.py（透传 `index_name`）
- src/app/services/constituent_service.py（注入 sources + 按需落库）
- src/app/services/percentile_service.py（注入 valuation_sources + 历史按需落库）
- src/app/core/di.py（装配 constituent_sources / valuation_sources）
- src/app/scheduler/jobs.py（透传 index_name / 用解析后的代码）
- src/app/templates/index.html（规模→亿元、费率→% 格式化）
- docs/PRD.md / SRS.md（数据源与字段说明微调）

## 3. 实施步骤（拆解）
- [x] 探测各 akshare 源可用性（见上表）
- [x] 重写 akshare_src：basic/valuation(get_latest+get_history)/constituent + 解析助手
- [x] 更新 interfaces（协议签名）
- [x] 更新 csindex_src / em_src（签名 + 东财校验）
- [x] valuation_service 透传 index_name
- [x] constituent_service 注入 sources + 按需落库
- [x] percentile_service 注入 valuation_sources + 历史按需落库
- [x] di.py 装配
- [x] jobs.py 同步
- [x] 前端数值格式化
- [x] 修复 akshare 同进程污染（子进程隔离 `_akshare_bridge`/`_runner`，见 bug-002 R1）
- [x] 修复 `_to_float(_pick(...))` 列名 bug + `_to_date` 兼容 ISO 8601（bug-002 R2/R3）
- [x] pytest 全量通过（20 passed）+ 探测 510300 四区块真实数据

## 4. 测试计划
- 单测：保留 task-01 的按月分位测试；新增"按需落库"路径的假源测试（constituent/valuation 空库→触发 sources→落库）。
- 集成验证：起 uvicorn，curl `/api/etf/510300/basic|valuation|percentile|constituents`，确认均返回真实数据。
- lint：改动 Python 文件零告警。

## 5. 风险与回滚
- 风险1：行业/主题类 ETF 的"按名历史源"不支持 → 分位优雅显示"暂无"（非崩溃）。
- 风险2：东财代理在本机不通 → 仅 akshare/CSIndex 主路径生效，已验证可用。
- 回滚：数据源改动彼此独立；若某源异常，多源降级自动跳过，不影响主流程。

## 附录：探测证据（节选）
- `fund_overview_em("510300")`：基金管理人=华泰柏瑞基金，最新规模=1,999.14亿元，管理费=0.15%/年，托管费=0.05%/年，投资标的=沪深300指数。
- `stock_zh_index_value_csindex("000300")`：市盈率1=14.79，市净率1=2.65（2026-07-10）。
- `stock_index_pe_lg("沪深300")`：5164 行完整历史（含静态市盈率/市净率/百分位）；但 `stock_index_pe_lg("中证白酒")` KeyError（仅主流宽基）。
- `index_stock_cons_csindex("000300")`：300 条成分股（代码/名称/交易所），需代码，名调用报错。
- `index_code_id_map_em()`：本机东财代理 ProxyError（用户机器可能可用，作运行时增强）。
