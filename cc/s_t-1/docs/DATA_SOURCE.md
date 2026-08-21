# 数据源说明

本软件使用**东方财富的公开网页行情接口**——这也是 [AKShare](https://github.com/akfamily/akshare)、
[efinance](https://github.com/Micro-sheep/efinance)、[qstock](https://github.com/tkfy920/qstock)
等主流开源项目所采用的同一套端点。选择它的原因：

- 免费、免注册、无 Token（`suggest` 接口的 token 是东财前端硬编码的公共常量，非个人凭证）；
- 同时覆盖 **A 股（沪/深/北）与港股**，且提供港股的主要财务指标；
- 历史日线可回溯 10 年以上，满足十年分位数的需求。

> 所有接口都被封装在 `StockAnalyzer.DataSource` 项目中，通过 `IStockDataSource` 抽象暴露。
> 更换数据源不会影响其它任何一层。

---

## 1. 接口清单

| 用途 | 端点 | 说明 |
|------|------|------|
| 在线模糊检索 | `https://searchapi.eastmoney.com/api/suggest/get` | 支持代码 / 中文名 / 拼音首字母，直接返回 `QuoteID`（即 secid）与 `PinYin` |
| 批量实时快照 | `https://push2.eastmoney.com/api/qt/ulist.np/get` | 一次可查多只，`fltt=2` 时返回已缩放的浮点数 |
| 全市场列表 | `https://push2.eastmoney.com/api/qt/clist/get` | 分页拉取全量代码；**限流较严**，属于可选优化 |
| 历史日线 | `https://push2his.eastmoney.com/api/qt/stock/kline/get` | `klt=101` 日线、`fqt=0` 不复权 |
| A 股财报 | `https://datacenter-web.eastmoney.com/api/data/v1/get` + `RPT_LICO_FN_CPD` | 业绩报表，含 `BASIC_EPS`、`BPS`、`NOTICE_DATE` |
| 港股财报 | `https://datacenter-web.eastmoney.com/api/data/v1/get` + `RPT_HKF10_FN_MAININDICATOR` | 主要财务指标，**直接提供 `EPS_TTM` 与 `BPS`** |

---

## 2. 字段口径（重要）

实时快照使用 `fltt=2`，接口直接返回浮点数，**无需按 `f59` 二次缩放**：

| 字段 | 含义 | 备注 |
|------|------|------|
| `f2` | 最新价 | |
| `f3` / `f4` | 涨跌幅% / 涨跌额 | |
| `f5` / `f6` | 成交量 / 成交额 | |
| `f7` / `f8` | 振幅% / 换手率% | |
| `f9` | **市盈率（动态）** | 最新报告期业绩**年化**推算，不是 TTM |
| `f12` / `f13` / `f14` | 代码 / 市场号 / 名称 | |
| `f15` / `f16` / `f17` / `f18` | 最高 / 最低 / 今开 / 昨收 | |
| `f20` / `f21` | 总市值 / 流通市值 | |
| `f23` | **市净率 PB** | |
| `f114` | **市盈率（静态）** | 基于最近一期年报 |
| `f115` | **市盈率（TTM）** | 滚动 12 个月，**本软件的分位数与通道均基于此口径** |

> 这三个 PE 字段极易混淆。实测验证（2026-08-21 收盘数据）：
>
> | 标的 | f9(动态) | f114(静态) | f115(TTM) | 本软件按财报自算的 TTM |
> |------|---------|-----------|-----------|---------------------|
> | 贵州茅台 600519 | 17.87 | 19.33 | 19.54 | **19.57** |
> | 工商银行 601398 | 7.96 | 7.51 | 7.46 | 7.69 |
>
> 自算 TTM 与 `f115` 高度吻合，确认 `f115` 才是 TTM 口径。

### 市场号（`f13` / `MktNum`）映射

| 值 | 市场 |
|----|------|
| 0 | 深交所（代码为 43/83/87/88/92 开头时判定为北交所） |
| 1 | 上交所 |
| 116 / 128 | 港交所 |

secid 格式为 `市场号.代码`，例如 `1.600519`、`0.000001`、`116.00700`。

### K 线字段顺序

`klines` 数组中每条为逗号分隔的字符串：

```
日期, 开盘, 收盘, 最高, 最低, 成交量, 成交额, 振幅, 涨跌幅, 涨跌额, 换手率
```

本软件固定使用 `fqt=0`（**不复权**），以便与「同期财报的每股指标」保持同一股本口径。

---

## 3. 限流与降级

东财对不同端点的限流强度差异很大。实测结论：

| 端点 | 限流强度 | 降级策略 |
|------|---------|---------|
| `clist`（全量列表） | **严格**，连续几次请求就会触发连接重置 | 分页间隔 800ms；失败即放弃并保留本地旧列表，**检索自动改走 `suggest`** |
| `suggest`（检索） | 宽松 | 250ms 去抖；失败时仅返回本地结果 |
| `ulist.np`（快照） | 宽松 | 失败时回退到本地上次快照 |
| `kline`（日线） | 宽松 | 失败时使用本地已缓存区间 |
| `datacenter`（财报） | 宽松 | 失败时估值序列降级为「近似模式」 |

正因为 `clist` 不可靠，本软件把**在线检索（`suggest`）作为主检索通道**，
全量列表只是「锦上添花」的离线加速，在后台静默尝试，失败完全不影响使用。

统一的稳定性措施（`EastmoneyHttpClient`）：

- 全局并发上限（默认 4，可配置）；
- 429 / 503 与网络异常线性退避重试（默认 2 次）；
- 浏览器 UA + `Referer`，并开启 gzip/deflate/brotli 自动解压（缺少解压会导致响应被提前截断）。

---

## 4. 可配置项

`src/StockAnalyzer.Desktop/appsettings.json`：

```jsonc
{
  "DataSource": {
    "TimeoutSeconds": 20,               // 单请求超时
    "RetryCount": 2,                    // 重试次数
    "RetryBaseDelayMilliseconds": 400,  // 退避基数（按次数线性放大）
    "MaxConcurrentRequests": 4,         // 并发上限
    "ListPageSize": 1000,               // 全量列表分页大小
    "QuoteBatchSize": 50                // 批量快照单次股票数
  }
}
```

可另建 `appsettings.Local.json` 覆盖本地配置，该文件已被 `.gitignore` 忽略。

---

## 5. 如何接入新的数据源

1. 在 `StockAnalyzer.DataSource`（或新项目）中实现 `IStockDataSource` 的 6 个方法：

   ```csharp
   string Name { get; }
   Task<IReadOnlyList<StockInfo>>      GetStockListAsync(MarketGroup, CancellationToken);
   Task<IReadOnlyList<StockInfo>>      SearchAsync(string, MarketGroup, int, CancellationToken);
   Task<StockQuote?>                   GetQuoteAsync(StockInfo, CancellationToken);
   Task<IReadOnlyList<StockQuote>>     GetQuotesAsync(IReadOnlyList<StockInfo>, CancellationToken);
   Task<IReadOnlyList<DailyBar>>       GetDailyHistoryAsync(StockInfo, DateTime, DateTime, CancellationToken);
   Task<IReadOnlyList<FinancialReport>>GetFinancialReportsAsync(StockInfo, CancellationToken);
   ```

2. 在 `App.BuildHost()` 中把 `AddEastmoneyDataSource()` 换成你的注册方法。

约定：

- 日线必须是**不复权**收盘价；
- 财报的 `BasicEps` 是**年初至报告期累计**口径；若数据源能直接给出滚动 TTM，
  请填 `EpsTtm`，构建器会优先使用它；
- `NoticeDate` 用于防止未来函数，缺失时可留空（构建器会按报告期 +45 天保守估计）；
- 取不到数据时返回**空集合而不是抛异常**，让上层走降级路径。

---

## 6. 使用礼仪

- 这些是面向网页前端的公开接口，不是官方开放平台。请**克制请求频率**，
  不要把本软件改造成高频轮询或批量爬取工具。
- 商业用途请改用有正式授权的数据服务（Wind、聚宽、Tushare Pro 等）。
