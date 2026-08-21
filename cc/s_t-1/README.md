# A股 / 港股 估值分析终端

一个基于 **C# + WPF (.NET 8)** 的 Windows 桌面股票分析软件，聚焦「**估值**」而非「盯盘」：
支持 A 股（沪 / 深 / 北）与港股的个股检索、自选管理、基本面速览、
**PE / PB 历史分位数**与**估值通道**（对标东方财富 App 的同名功能）。

所有查询过的数据都会持久化在本地 SQLite，二次打开秒开、断网也能看历史分析。

---

## 功能一览

| # | 需求 | 实现情况 |
|---|------|---------|
| 1 | 开源稳定的数据源 | 东方财富公开行情接口（`push2` / `push2his` / `datacenter-web` / `searchapi`），与 AKShare、efinance 等开源项目使用的是同一套端点；数据源以接口抽象隔离，可替换 |
| 2 | C# 桌面软件 | .NET 8 + WPF + MVVM（CommunityToolkit.Mvvm），图表使用 ScottPlot 5 |
| 3 | 查询数据本地持久化 | EF Core + SQLite，缓存股票列表、行情快照、日线、财报，并带 TTL 与增量更新 |
| 4 | 按编号 / 名称模糊查询 + 加自选 | 支持**代码 / 中文名称 / 拼音首字母**三种模糊匹配（本地索引 + 在线检索兜底），双击或点按钮加入自选 |
| 5 | 个股基本信息 | 股价、涨跌、振幅、换手、成交额、总市值 / 流通市值、总股本、PE(TTM/静态/动态)、PB、每股净资产 |
| 6 | 1/3/5/10 年 PE、PB 历史分位数 | 逐日估值序列 + 分位数表（当前值、百分位、最小/30%/中位/70%/最大、样本数、覆盖度、估值温度） |
| 7 | 1/3/5/10 年估值通道 | 10%/30%/50%/70%/90% 分位倍数 × 逐日每股锚定值，折算成与股价同坐标的价格带 |
| 8 | 文档 / 编译脚本 / gitignore | `docs/`、`build/`、`.gitignore` 齐备 |

---

## 界面结构

```
┌─────────────────────────────────────────────────────────────────┐
│ 搜索框 │ 市场筛选 │       │ 同步列表 │ 刷新自选 │ 清理缓存        │
├──────────────┬──────────────────────────────────────────────────┤
│ 搜索结果      │  名称 / 代码   最新价 涨跌幅      [加入自选][刷新] │
│ 我的自选      ├──────────────────────────────────────────────────┤
│              │  基本信息 │ 历史分位 │ 估值通道                    │
│              │                                                  │
│              │      指标卡片 / 分位表 / 走势图 / 通道图           │
├──────────────┴──────────────────────────────────────────────────┤
│ 状态信息                       本地数据统计            数据源     │
└─────────────────────────────────────────────────────────────────┘
```

---

## 快速开始

### 环境要求

- Windows 10 1809 或更高版本
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)（开发）
  或 .NET 8 Desktop Runtime（仅运行依赖框架版本）

### 编译并运行

```powershell
# 还原 + 编译 + 测试
./build/build.ps1

# 直接运行
dotnet run --project src/StockAnalyzer.Desktop
```

### 发布

```powershell
# 依赖框架版本（体积小，需目标机器安装 .NET 8 Desktop Runtime）
./build/publish.ps1

# 自包含单文件版本（免安装运行时）
./build/publish.ps1 -SelfContained -SingleFile
```

产物输出到 `artifacts/win-x64/`。

### 清理

```powershell
./build/clean.ps1                    # 清理 bin/obj/artifacts
./build/clean.ps1 -IncludeLocalData  # 同时删除本地数据库（自选股会丢失）
```

---

## 项目结构

```
StockAnalyzer.sln
├─ src/
│  ├─ StockAnalyzer.Core/         领域模型、接口抽象、估值算法（无外部依赖）
│  │  ├─ Models/                  StockInfo / StockQuote / DailyBar / FinancialReport / ValuationSeries …
│  │  ├─ Abstractions/            IStockDataSource / IStockRepository
│  │  ├─ Analytics/               PercentileCalculator / ValuationSeriesBuilder / ValuationAnalyzer
│  │  ├─ Services/                StockService（缓存优先 + 按需回源的编排层）
│  │  └─ Utils/                   SecurityIdHelper / PinyinHelper
│  ├─ StockAnalyzer.Data/         EF Core + SQLite 持久化实现
│  ├─ StockAnalyzer.DataSource/   东方财富接口客户端
│  └─ StockAnalyzer.Desktop/      WPF 界面（MVVM）
├─ tests/StockAnalyzer.Tests/     算法与工具类单元测试（xUnit）
├─ build/                         编译 / 发布 / 清理脚本
└─ docs/                          详细文档
```

依赖方向严格单向：`Desktop → Data / DataSource → Core`，Core 不依赖任何具体实现。

---

## 文档索引

| 文档 | 内容 |
|------|------|
| [docs/USER_GUIDE.md](docs/USER_GUIDE.md) | 使用手册：检索、自选、各页面含义与解读方法 |
| [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) | 架构设计、分层职责、数据流、缓存策略、数据库表结构 |
| [docs/DATA_SOURCE.md](docs/DATA_SOURCE.md) | 数据源接口清单、字段口径、限流与降级策略、如何替换数据源 |
| [docs/VALUATION.md](docs/VALUATION.md) | 估值算法：TTM 还原、时点对齐、分位数口径、估值通道构造、已知局限 |
| [docs/DEVELOPMENT.md](docs/DEVELOPMENT.md) | 开发指南：环境、编码规范、调试、测试、常见问题 |

---

## 数据与免责声明

- 本软件通过**公开的网页行情接口**获取数据，仅供个人学习与研究使用。
- 请遵守数据提供方的服务条款，**不要高频轮询**；软件已内置并发限制、重试退避与本地缓存。
- 估值计算涉及会计口径假设（详见 [docs/VALUATION.md](docs/VALUATION.md)），结果**不构成任何投资建议**。
- 历史分位数只描述过去，不预测未来；低分位不等于便宜，高分位不等于必跌。

## 许可证

本仓库供个人研究使用。第三方依赖遵循各自的开源许可证（MIT / Apache-2.0 等）。
