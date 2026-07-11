# 变更日志

## 2026-07-11 - 修复 K线数据获取失败 + 跟踪指数识别

### 问题 1: K线数据 `unexpected keyword argument 'symbol'`

#### 复现
查询任意 ETF → 数据获取错误: `got an unexpected keyword argument 'symbol'`

#### 根因
`_safe_fetch` 函数中调用 `fetch_fn(**kwargs)`，将日志参数传给了无参 lambda：
```python
# 错误：kwargs={symbol: "510050"} 传给了 lambda: 无参函数
df = fetch_fn(**kwargs)
```

#### 修复
`_safe_fetch` 改为 `fetch_fn()`，日志参数仅用于记录，不传给可调用对象。

### 问题 2: 未找到跟踪指数代码

#### 根因
雪球 API 返回的详细字段中不一定包含跟踪指数信息，之前的匹配逻辑过于简单。

#### 修复
新增 `_resolve_index_code()` 方法，双层策略：
1. 从雪球详细信息中查找含"跟踪"/"标的"/"基准"关键词的字段
2. 从 ETF 名称中解析指数名（如 "华泰柏瑞沪深300ETF" → "沪深300"）
3. 内置 **100+ 常见指数名称→指数代码映射表**（沪深300/上证50/科创50/创业板/中证500/恒生科技/纳斯达克...）

---

## 2026-07-11 - 修复 akshare API 接口变更导致 ETF 列表为空

### 问题复现
- 启动后搜索 ETF 返回 0 条结果
- 日志显示：`ETF 列表列名不匹配，实际列: ['净值日期', '单位净值', ...]`

### 根因分析
- `ak.fund_etf_fund_info_em()` 在最新版 akshare 中已变为 **净值历史数据** 接口
- 返回的是净值时间序列，不包含 基金代码/基金简称 列
- 需要换用 `ak.fund_etf_spot_em()` 获取 ETF 列表（代码+名称+行情）

### 修复内容

#### API 层 (`src/api/etf_data.py`)
- `get_all_etf_list()`: 改用 `ak.fund_etf_spot_em()`，返回列：代码, 名称, 最新价, 涨跌幅 等
- 新增 `get_etf_detailed_info(code)`: 使用 `ak.fund_individual_basic_info_xq()`（雪球）获取详细信息（基金管理人、成立日期、费率、规模等）
- `get_etf_holdings()`: 失败时返回空 DataFrame 而非抛异常（由上层决定展示）
- `get_etf_dividend_info()`: 新增 `fund_etf_dividend_sina` 数据源

#### Service 层 (`src/services/etf_service.py`)
- `search_etf_by_keyword()`: 列名适配（代码/名称），去重改用 `subset=[代码]`
- `get_etf_detail()`: 使用新的 `get_etf_detailed_info()` 获取详细信息

### 验证结果
- 4 个核心文件全部通过语法检查

---

## 2026-07-11 - 项目初始化与完整实现

### 设计阶段
- 确定技术栈：Python 3.11 + Streamlit + akshare + Plotly
- 三层架构设计：UI → Service → API
- 模块划分完成

### 规划阶段
- ETF 数据查询流程设计（代码精确匹配 → 名称模糊搜索）
- UI 布局设计（侧边栏搜索 + Tab 展示）
- PE/PB 分位数计算方案（基于历史数据窗口）
- 错误处理策略：不隐藏错误，每层向上抛出，UI 层汇总展示

### 实施阶段

#### 文档
- `docs/dev-constraints.md` - 开发约束文档（错误处理、开发流程、架构约束、文档约束、代码规范、稳定性要求）
- `docs/architecture.md` - 架构设计文档（三层架构、数据流、模块说明、技术栈）
- `docs/changelog.md` - 变更日志
- `README.md` - 项目说明

#### API/Data 层 (`src/api/etf_data.py`)
- `get_all_etf_list()` - 全量 ETF 列表（来自东方财富）
- `get_etf_real_time_data()` - 实时行情
- `get_etf_hist_data()` - K线历史数据（前复权）
- `get_etf_holdings()` - 成分股持仓（多数据源自动切换容错）
- `get_index_valuation_hist()` - 指数 PE/PB 历史数据
- `get_etf_dividend_info()` - 分红记录
- `_safe_fetch()` - 统一异常处理包装器

#### Service 层
- `src/services/etf_service.py` - ETF 数据聚合服务
  - `load_etf_list()` - 带缓存的 ETF 列表加载
  - `search_etf_by_keyword()` - 代码/名称模糊搜索
  - `get_etf_detail()` - 获取完整 ETF 详情（并发获取所有数据，错误不中断）
- `src/services/valuation_service.py` - 估值计算服务
  - `calc_percentile()` - 分位数计算
  - `calc_percentiles_for_windows()` - 多窗口分位数（3/5/10/20年）
  - `get_current_pe_pb_from_latest()` - 提取最新估值

#### UI 层
- `src/ui/components.py` - UI 组件库
  - `render_kline_chart()` - K线图（OHLC标准K线 + 成交量，或净值折线图）
  - `render_basic_info()` - 基本信息卡片
  - `render_holdings()` - 成分股持仓表格
  - `render_valuation()` - 估值展示（当前PE/PB + 分位数表格 + 历史走势图）
  - `render_pe_pb_percentile_table()` - 分位数表格（带颜色标注）
  - `render_pe_pb_history_chart()` - PE/PB 历史走势图
  - `render_dividends()` - 分红记录表格
- `app.py` - Streamlit 主应用（侧边栏搜索 + Tab 详情展示）

#### 基础设施
- `start.bat` - 一键启动脚本（自动检测 Python → 创建虚拟环境 → 安装依赖 → 启动服务）
- `requirements.txt` - Python 依赖
- 包 `__init__.py` 文件

### 质量保证
- 全部 5 个 Python 源文件通过 `py_compile` 语法检查
- Type hints 完整
- 错误不隐藏，统一通过日志记录，UI 层汇总展示
- 外部 API 调用有时间预估，异常有合理的降级策略
