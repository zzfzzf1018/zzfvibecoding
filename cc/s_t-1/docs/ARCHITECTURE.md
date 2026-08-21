# 架构设计

## 1. 分层与依赖方向

```
┌──────────────────────────────────────────────┐
│  StockAnalyzer.Desktop  (WPF / MVVM)         │
│  Views · ViewModels · Charts · Converters    │
└───────────────┬──────────────────────────────┘
                │ 依赖注入（Microsoft.Extensions.Hosting）
    ┌───────────┴───────────┐
    ▼                       ▼
┌────────────────┐  ┌──────────────────────┐
│ StockAnalyzer  │  │ StockAnalyzer        │
│ .Data          │  │ .DataSource          │
│ EF Core+SQLite │  │ Eastmoney HTTP       │
│ IStockRepository│ │ IStockDataSource     │
└───────┬────────┘  └──────────┬───────────┘
        │                      │
        ▼                      ▼
┌──────────────────────────────────────────────┐
│  StockAnalyzer.Core                          │
│  Models · Abstractions · Analytics · Service │
│  （纯 .NET，无第三方依赖）                     │
└──────────────────────────────────────────────┘
```

规则：

- `Core` 只定义模型、接口与算法，**不引用**任何具体实现，因此算法可脱离网络与数据库单测。
- `Data` 与 `DataSource` 分别实现 `IStockRepository`、`IStockDataSource`，互不感知。
- `Desktop` 只依赖 `StockService` 与模型，不直接触碰 HTTP 或 SQL。

替换数据源（例如改用新浪 / 腾讯 / 自建服务）只需新增一个 `IStockDataSource` 实现并在 DI 中注册，
其余代码零改动。

---

## 2. 关键类型

| 类型 | 职责 |
|------|------|
| `IStockDataSource` | 数据源抽象：列表、在线检索、快照、日线、财报 |
| `IStockRepository` | 持久化抽象：股票列表、自选股、快照、日线、财报、同步时间戳 |
| `StockService` | **编排层**。实现「本地缓存优先 + 按需回源 + 失败降级」，是 UI 唯一入口 |
| `ValuationSeriesBuilder` | 把日线与财报合成逐日 PE/PB 序列（含时点对齐与快照校准） |
| `ValuationAnalyzer` | 分位数统计与估值通道构造 |
| `PercentileCalculator` | 百分位与分位数的底层数学实现 |
| `MainViewModel` | 检索、自选、全局状态 |
| `StockDetailViewModel` | 个股详情三个页签的数据装配 |

---

## 3. 数据流

### 3.1 模糊检索

```
用户输入
   │ 250ms 去抖
   ▼
StockService.SearchAsync
   ├─ ① 本地 SQLite LIKE 检索（代码 / 名称 / 拼音首字母）── 毫秒级，可离线
   └─ ② 在线 suggest 接口检索（补充本地缺失的标的）
            │
            └─ 结果 UpsertStocksAsync 写回本地，下次即可离线命中
   ▼
合并去重 → 按相关性排序 → 返回
```

相关性排序优先级：代码全等 > 代码前缀 > 名称全等 > 名称前缀 > 拼音首字母前缀 > 名称包含。

### 3.2 个股分析

```
选中股票
   ▼
StockService.GetQuoteAsync            → 1 分钟内命中缓存，否则回源；回源失败退回缓存
   ▼
StockService.GetValuationSeriesAsync
   ├─ EnsureDailyBarsAsync            → 缺历史则整段拉取，仅缺尾部则增量拉取
   ├─ EnsureFinancialReportsAsync     → 7 天 TTL
   └─ ValuationSeriesBuilder.Build    → 本地计算逐日 PE/PB
   ▼
ValuationAnalyzer.CalculateAllWindows → 1/3/5/10 年分位数
ValuationAnalyzer.BuildChannel        → 估值通道
```

**所有统计计算都在本地完成**，切换窗口 / 指标不产生任何网络请求。

---

## 4. 缓存策略

| 数据 | 有效期 | 失效后的行为 |
|------|--------|-------------|
| 股票列表 | 3 天 | 后台异步重拉；失败保留旧数据，检索自动走在线接口 |
| 行情快照 | 1 分钟 | 回源刷新；失败回退到上次快照 |
| 日线 | 6 小时，且要求已覆盖到上一个交易日 | 缺历史→整段重取；仅缺尾部→从最后一天前 5 天增量拉取 |
| 财报 | 7 天 | 回源刷新；即使返回空也写时间戳，避免反复请求不支持的市场 |

「上一个交易日」的判定只跳过周末，并在 16:00 前额外向前顺延一天；
法定节假日由「数据源返回为空」自然兜底，不维护交易日历。

---

## 5. 数据库结构（SQLite）

| 表 | 主键 | 说明 |
|----|------|------|
| `Stocks` | (Market, Code) | 代码、名称、secid、拼音首字母；在 Code / Name / NameInitials 上建索引 |
| `Watchlist` | (Market, Code) | 自选股，含排序序号与备注 |
| `Quotes` | (Market, Code) | 最近一次行情快照 |
| `DailyBars` | (Market, Code, Date) | 不复权日线 |
| `FinancialReports` | (Market, Code, ReportDate) | 每股财务指标 |
| `SyncStamps` | Key | 各类数据的最近同步时间 |

- 数据库默认位于 `%LOCALAPPDATA%\StockAnalyzer\stock.db`，可通过 `appsettings.json` 的
  `Storage:DatabasePath` 改为绿色便携模式（填相对路径即可，基于程序目录解析）。
- 启用 `journal_mode=WAL` 与 `synchronous=NORMAL`，兼顾写入速度与读并发。
- 通过 `IDbContextFactory<StockDbContext>` 为每次操作创建独立上下文，避免 WPF 多任务并发时
  共享 `DbContext` 引发的线程问题。

---

## 6. 并发与稳定性

- `EastmoneyHttpClient` 用 `SemaphoreSlim` 限制全局并发（默认 4），并对 429/503 与网络异常
  做线性退避重试（默认 2 次）。
- 全量列表接口分页间强制 800ms 间隔，降低被限流的概率。
- 详情页加载使用 `CancellationTokenSource`：用户快速切换标的时，旧请求会被取消。
- 检索使用 250ms 去抖，避免逐字符触发查询。
- 所有网络失败路径都有本地降级，界面不会因为断网而不可用。

---

## 7. 界面实现要点

- MVVM 使用 `CommunityToolkit.Mvvm` 的源生成器（`[ObservableProperty]` / `[RelayCommand]`）。
- 图表封装为两个 `UserControl`，通过依赖属性接收数据后重绘，保持 ViewModel 与绘图库解耦：
  - `ValuationChannelChart`：股价 + 分位价格带
  - `ValuationHistoryChart`：PE/PB 走势 + 分位水平线
- ScottPlot 默认字体不含中文，统一由 `ChartStyler.ApplyChineseFont` 在图元添加完毕后处理。
- 深色主题集中在 `Themes/Theme.xaml`，涨红跌绿通过 `ChangeToBrushConverter` 实现。
- 进度文本使用 `ImmediateProgress<T>` 而非 `Progress<T>`，避免异步投递导致最后一条中间提示
  覆盖「加载完成」状态。
