
# 大A ETF工具 - 设计文档

## 1. 架构设计

### 1.1 整体架构

本项目采用分层架构，分为三层：

```
┌─────────────────────────────────────────────────────────┐
│                    EtfTool.Wpf                          │
│                    (展示层)                              │
│  ┌───────────────┐  ┌───────────────┐  ┌───────────┐   │
│  │ MainWindow    │  │ EtfDetailView │  │ Converters│   │
│  │ MainViewModel │  │DetailViewModel│  │           │   │
│  └───────────────┘  └───────────────┘  └───────────┘   │
└──────────────────────────┬──────────────────────────────┘
                           │
                           ▼
┌─────────────────────────────────────────────────────────┐
│                    EtfTool.Data                          │
│                    (数据访问层)                          │
│  ┌───────────────┐  ┌───────────────┐  ┌───────────┐   │
│  │ ApiClients    │  │   Providers   │  │   Cache   │   │
│  │ Sina/EastMoney│  │ Factory/Cached│  │  SQLite   │   │
│  └───────────────┘  └───────────────┘  └───────────┘   │
│  ┌───────────────┐                                      │
│  │ EtfService   │  ← 核心业务服务                        │
│  └───────────────┘                                      │
└──────────────────────────┬──────────────────────────────┘
                           │
                           ▼
┌─────────────────────────────────────────────────────────┐
│                    EtfTool.Core                          │
│                    (核心层)                              │
│  ┌───────────────┐  ┌───────────────┐  ┌───────────┐   │
│  │    Models     │  │   Interfaces  │  │   Enums   │   │
│  │ EtfInfo等     │  │ IEtfDataProvider│ │DataSource│   │
│  └───────────────┘  └───────────────┘  └───────────┘   │
└─────────────────────────────────────────────────────────┘
```

### 1.2 模块职责

| 模块 | 职责 |
| :--- | :--- |
| EtfTool.Core | 定义数据模型、接口和枚举，为其他层提供基础 |
| EtfTool.Data | 实现 API 客户端、缓存管理、数据提供者和核心业务服务 |
| EtfTool.Wpf | WPF 界面展示，实现 MVVM 模式 |

## 2. 数据模型设计

### 2.1 EtfInfo

| 字段名 | 类型 | 说明 |
| :--- | :--- | :--- |
| Code | string | ETF 代码 |
| Name | string | ETF 简称 |
| FullName | string | ETF 全称 |
| Type | string | ETF 类型 |
| Exchange | string | 交易所 |
| TotalAssets | decimal? | 总资产 |
| Unit | decimal? | 基金份额 |
| LatestPrice | decimal? | 最新净值 |
| ChangePercent | decimal? | 涨跌幅 |
| PeRatio | decimal? | PE |
| PbRatio | decimal? | PB |
| DividendYield | decimal? | 分红收益率 |
| ManagementFee | decimal? | 管理费 |
| CustodyFee | decimal? | 托管费 |
| SalesServiceFee | decimal? | 销售服务费 |
| SubscriptionFee | decimal? | 申购费 |
| RedemptionFee | decimal? | 赎回费 |
| ListedDate | DateTime? | 成立日期 |
| UpdateTime | DateTime? | 更新时间 |

### 2.2 EtfComponent

| 字段名 | 类型 | 说明 |
| :--- | :--- | :--- |
| EtfCode | string | ETF 代码 |
| StockCode | string | 股票代码 |
| StockName | string | 股票名称 |
| Weight | decimal? | 权重（%） |
| Price | decimal? | 价格 |
| ChangePercent | decimal? | 涨跌幅 |
| PeRatio | decimal? | PE |
| PbRatio | decimal? | PB |
| Rank | int? | 排名 |
| UpdateTime | DateTime? | 更新时间 |

### 2.3 KlineData

| 字段名 | 类型 | 说明 |
| :--- | :--- | :--- |
| EtfCode | string | ETF 代码 |
| Date | DateTime | 日期 |
| Open | decimal | 开盘价 |
| High | decimal | 最高价 |
| Low | decimal | 最低价 |
| Close | decimal | 收盘价 |
| Volume | decimal | 成交量 |
| Amount | decimal | 成交额 |
| PeRatio | decimal? | PE |
| PbRatio | decimal? | PB |
| ChangePercent | decimal? | 涨跌幅 |

### 2.4 EtfStatistics

| 字段名 | 类型 | 说明 |
| :--- | :--- | :--- |
| EtfCode | string | ETF 代码 |
| CurrentPe | decimal? | 当前 PE |
| CurrentPb | decimal? | 当前 PB |
| AvgPe | decimal? | PE 均值 |
| AvgPb | decimal? | PB 均值 |
| PeMin | decimal? | PE 最小值 |
| PeMax | decimal? | PE 最大值 |
| PbMin | decimal? | PB 最小值 |
| PbMax | decimal? | PB 最大值 |
| PePercentile | decimal? | PE 分位数（%） |
| PbPercentile | decimal? | PB 分位数（%） |
| DataCount | int? | 数据条数 |
| StartDate | DateTime? | 开始日期 |
| EndDate | DateTime? | 结束日期 |

### 2.5 EtfDividend

| 字段名 | 类型 | 说明 |
| :--- | :--- | :--- |
| EtfCode | string | ETF 代码 |
| DividendDate | DateTime? | 分红日期 |
| DividendPerUnit | decimal? | 每份分红 |
| DividendType | string | 分红类型 |
| Remark | string | 备注 |

## 3. 接口设计

### 3.1 IEtfDataProvider

```csharp
public interface IEtfDataProvider
{
    Task<EtfInfo?> GetEtfInfoAsync(string etfCode);
    Task<List<EtfComponent>> GetEtfComponentsAsync(string etfCode);
    Task<List<KlineData>> GetKlineDataAsync(string etfCode, string period = "day", int count = 120);
    Task<List<EtfDividend>> GetEtfDividendsAsync(string etfCode);
    Task<List<EtfInfo>> SearchEtfAsync(string keyword);
}
```

### 3.2 ICacheManager

```csharp
public interface ICacheManager
{
    Task<T?> GetFromCacheAsync<T>(string key) where T : class;
    Task SaveToCacheAsync<T>(string key, T data, TimeSpan? expiration = null) where T : class;
    Task ClearCacheAsync(string key);
    Task ClearAllCacheAsync();
    // ETF 专用缓存方法...
}
```

## 4. API 接口设计

### 4.1 新浪财经 API

| 接口 | URL | 说明 |
| :--- | :--- | :--- |
| 实时行情 | `https://hq.sinajs.cn/list={market}{code}` | 获取 ETF 实时数据 |
| K线数据 | `https://finance.sina.com.cn/stock/api/jsonp.php/...` | 获取 K 线数据 |
| 搜索建议 | `https://suggest.sinajs.cn/suggest/type=11&key={keyword}` | 搜索 ETF |

### 4.2 东方财富 API

| 接口 | URL | 说明 |
| :--- | :--- | :--- |
| 实时行情 | `https://push2.eastmoney.com/api/qt/stock/get` | 获取 ETF 实时数据 |
| K线数据 | `https://push2his.eastmoney.com/api/qt/stock/kline/get` | 获取 K 线数据 |
| 成分股 | `https://datacenter.eastmoney.com/api/data/v1/get` | 获取成分股数据 |
| 分红信息 | `https://datacenter.eastmoney.com/api/data/v1/get` | 获取分红记录 |
| 搜索 | `https://searchapi.eastmoney.com/suggest/get` | 搜索 ETF |

## 5. 缓存策略

### 5.1 缓存过期时间

| 数据类型 | 过期时间 | 缓存键 |
| :--- | :--- | :--- |
| ETF基本信息 | 24小时 | `etf_info_{code}` |
| 成分股数据 | 7天 | `etf_components_{code}` |
| K线数据（日线） | 长期 | `kline_day_{code}` |
| K线数据（周线） | 长期 | `kline_week_{code}` |
| K线数据（月线） | 长期 | `kline_month_{code}` |

### 5.2 缓存更新策略

1. 优先从缓存读取数据
2. 缓存不存在或过期时从网络获取
3. 获取成功后更新缓存

## 6. 估值计算算法

### 6.1 PE/PB 分位数计算

```
1. 获取历史 K 线数据（默认3年，约750个交易日）
2. 过滤有效 PE/PB 值（> 0）
3. 排序后计算当前值的分位数
4. 分位数 = (当前值以下的数据条数 / 总数据条数) * 100
```

### 6.2 成分股加权 PE/PB

```
加权 PE = Σ(成分股权重 * 成分股 PE) / 100
加权 PB = Σ(成分股权重 * 成分股 PB) / 100
```

## 7. UI 设计

### 7.1 主窗口布局

```
┌─────────────────────────────────────────────────────────────┐
│ [搜索框]                    [搜索按钮] [数据源] [清除缓存]   │
├─────────────────────────────────────────────────────────────┤
│ ┌──────────────────┐ ┌───────────────────────────────────┐ │
│ │   搜索结果列表    │ │         ETF 详情区域               │ │
│ │                  │ │  ┌─────────────────────────────┐  │ │
│ │  510300 沪深300   │ │  │ 基本信息 | 成分股 | K线    │  │ │
│ │  510500 中证500   │ │  │ 估值分析 | 分红信息        │  │ │
│ │  ...             │ │  └─────────────────────────────┘  │ │
│ └──────────────────┘ └───────────────────────────────────┘ │
└─────────────────────────────────────────────────────────────┘
```

### 7.2 详情页面 Tab 结构

| Tab 名称 | 内容 |
| :--- | :--- |
| 基本信息 | ETF 代码、名称、净值、涨跌幅、PE、PB 等 |
| 成分股 | 成分股列表、权重、PE、PB |
| K线 | K 线图表、MA5/MA10、周期切换 |
| 估值分析 | 当前 PE/PB、分位数、均值、区间 |
| 分红信息 | 分红日期、金额、类型 |

## 8. 依赖关系

### 8.1 项目依赖

```
EtfTool.Wpf ──→ EtfTool.Data ──→ EtfTool.Core
                    │
                    └──→ EtfTool.Core
```

### 8.2 NuGet 依赖

| 包名 | 版本 | 用途 |
| :--- | :--- | :--- |
| Newtonsoft.Json | 13.0.3 | JSON 解析 |
| Dapper | 2.1.24 | SQLite ORM |
| Microsoft.Data.Sqlite | 8.0.0 | SQLite 提供器 |
| LiveChartsCore | 2.0.0-rc1 | 图表核心 |
| LiveChartsCore.SkiaSharpView | 2.0.0-rc1 | SkiaSharp 视图 |
| LiveChartsCore.SkiaSharpView.WPF | 2.0.0-rc1 | WPF 图表绑定 |
| MahApps.Metro | 2.4.9 | WPF UI 美化 |
| MahApps.Metro.IconPacks | 4.11.0 | 图标库 |
| MvvmLight | 5.4.1.1 | MVVM 框架 |

## 9. 部署架构

### 9.1 运行环境

- Windows 10 或更高版本
- .NET 6.0 SDK 或更高版本

### 9.2 目录结构

```
EtfTool/
├── EtfTool.Wpf.exe          ← 主程序
├── EtfTool.Wpf.dll          ← 主程序集
├── EtfTool.Data.dll         ← 数据层
├── EtfTool.Core.dll         ← 核心层
├── Cache/
│   └── etf_cache.db         ← SQLite 缓存数据库
└── start.bat                ← 启动脚本
```

## 10. 版本历史

| 版本 | 日期 | 修改内容 | 作者 |
| :--- | :--- | :--- | :--- |
| 1.0 | 2026-07-09 | 初始版本 | AI Assistant |
