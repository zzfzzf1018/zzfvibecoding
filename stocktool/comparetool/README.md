# A 股财报对比工具 (ComparetoolWpf)

一个基于 **C# / WPF (.NET 8)** 的桌面应用，用于查询 A 股上市公司的三大财务报表，
并提供五类核心分析能力：

1. **选股器**：按 ROE / 毛利率 / 净利率 / 营收同比 / 净利同比 / EPS / 行业 等
   基本面条件批量筛选 A 股，结果可一键导出。
2. **单股期间对比**：选定一只股票，加载其多期（年报 / 中报 / 季报）报表，
   选择任意两期进行差异比较，自动高亮变化幅度超过阈值的指标。
3. **多股横向对比（百分比报表）**：选定多只股票，加载同一报告期的同一报表，
   将每个指标按"基准项"（资产总计 / 营业总收入 / 经营活动现金流入小计）
   折算成百分比，便于跨公司同口径比较。
4. **指标分析**：自动计算指定指标的同比 / 环比序列；
   ROE 杜邦三因素拆解（净利率 × 资产周转率 × 权益乘数）；
   营运资本变化、自由现金流（FCF）、现金转换周期（CCC = DSO + DIO − DPO）。
5. **趋势图（OxyPlot）**：多只股票同一指标的多期趋势曲线绘制。

易用性：
- 所有搜索框均为**输入即联想**（300ms 防抖，从下拉直接选）；
- 所有结果表格支持 **Ctrl+C** 复制选中区域；
- 所有结果均支持 **导出 Excel / CSV**；
- 报表数据使用 **SQLite 本地缓存**，重复加载不再调用接口。

> 数据来源：东方财富公开 F10 / 数据中心接口，无需注册或 API Key。

---

## 1. 运行环境

- Windows 10 / 11
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- 网络可访问 `eastmoney.com`

---

## 2. 快速开始

```powershell
# 克隆/进入项目目录后
cd comparetool
dotnet build
dotnet run --project ComparetoolWpf
```

也可直接使用 Visual Studio 2022 打开 `ComparetoolWpf.sln` 运行。

---

## 3. 功能详解

### 3.1 选股器

1. 进入「选股器」Tab，填写报告期（默认上一年度年报，例如 `2024-12-31`）。
2. 设置基本面阈值（留空 = 不限）：ROE / 毛利率 / 净利率 / 营收同比 / 净利同比 / EPS / 行业关键字。
3. 点击 **开始筛选**，下方表格显示符合条件的股票，可一键导出。
4. 数据来自东方财富业绩报告接口 `RPT_LICO_FN_CPD`，单次请求即可覆盖数百只股票。

### 3.2 单股期间对比

操作步骤：

1. 在“股票”输入框中输入名称片段或代码，点击 **搜索**。
2. 在下拉框中选择目标股票，选择 **报表类型**（资产负债表 / 利润表 / 现金流量表）
   与 **报告期类型**（年报 / 中报 / 一季报 / 三季报 / 全部）。
3. 点击 **加载历史报表**，工具默认拉取最近 20 期。
4. 在“基准期 / 对比期”下拉中选择两期，设置高亮阈值（默认 ±20%），
   点击 **对比**。
5. 结果表给出 “基准期数值 / 对比期数值 / 绝对变化 / 变化率”，
   超过阈值的行以浅红底色高亮，便于快速发现异常变动。

变化率公式：

$$
\text{ChangeRatio} = \frac{V_{\text{compare}} - V_{\text{base}}}{|V_{\text{base}}|}
$$

### 3.3 多股横向对比（百分比报表）
1. 搜索 → 选中股票 → 点击 **加入对比池**，重复以加入多只股票。
2. 选择 **报表类型** 与 **报告期类型**，点击 **加载并对比**。
3. 工具会拉取每只股票"最近一期"同类型报表，对每个指标计算占基准项的比例：

| 报表类型     | 基准项           |
|--------------|------------------|
| 资产负债表   | 资产总计         |
| 利润表       | 营业总收入       |
| 现金流量表   | 经营活动现金流入小计 |

每只股票生成两列：原始数值 + 百分比，可直接横向比较结构差异
（如毛利率、费用率、资产构成等）。

### 3.4 指标分析（同比 / 环比 / 杜邦 / 现金质量）

1. 搜索 → 选股 → 选择 **报告期类型**（建议年报） → **加载并分析**。
2. 顶部「同比/环比指标」下拉中选择任一指标，**同比/环比** 表显示：
   本期值 / 同比基期 / 同比 / 环比基期 / 环比。
3. **ROE 杜邦拆解**：
   $$
   \text{ROE} = \frac{\text{净利润}}{\text{营收}} \times \frac{\text{营收}}{\text{总资产}} \times \frac{\text{总资产}}{\text{股东权益}}
   $$
4. **现金质量与营运能力**：
   - 营运资本 = 流动资产 − 流动负债，及其逐期变化；
   - 自由现金流 FCF = 经营现金流 − |投资活动现金流出小计|；
   - DSO/DIO/DPO/CCC：
     $$ \text{CCC} = \text{DSO} + \text{DIO} - \text{DPO} $$
5. 三个表格分别支持 **导出 Excel / CSV**。

### 3.5 趋势图

1. 加入多只股票，选择 **报表 / 报告期类型 / 指标**。
2. 点击 **绘制趋势**，OxyPlot 控件渲染多曲线对比，便于查看指标历史走势。

### 3.6 缓存与离线复用

- 缓存数据库文件位置：`%LocalAppData%\ComparetoolWpf\reports.db`。
- 默认策略：本地有 ≥ 4 期且最新报告期不超过 1 年的，直接走缓存；
  否则调用接口刷新，并把新数据回写覆盖。
- 单股对比页面提供 **强制刷新(忽略缓存)** 复选框，按需绕过缓存。
- 选股器接口数据不入缓存（每次都拉取最新）。

---

## 4. 项目结构

```
ComparetoolWpf/
├─ App.xaml / App.xaml.cs              # 应用启动
├─ Models/                             # 数据模型
│   ├─ StockInfo.cs                    # 股票基本信息
│   ├─ FinancialReport.cs              # 单期报表
│   ├─ ComparisonRow.cs                # 对比结果行
│   └─ MetricsRows.cs                  # 同比/环比/杜邦行
├─ Services/
│   ├─ EastMoneyService.cs             # 东方财富数据访问（F10 真实接口）
│   ├─ ReportCache.cs                  # SQLite 本地缓存
│   ├─ StockDataService.cs             # 缓存优先门面
│   ├─ ComparisonService.cs            # 期间对比 / 同口径对比 计算
│   ├─ MetricsService.cs               # 同比/环比/ROE 杜邦
│   └─ ExportService.cs                # CSV / Excel 导出（ClosedXML）
├─ ViewModels/                         # MVVM 视图模型
│   ├─ MainViewModel.cs
│   ├─ SinglePeriodCompareViewModel.cs
│   ├─ MultiStockCompareViewModel.cs
│   ├─ MetricsViewModel.cs
│   └─ TrendChartViewModel.cs
├─ Views/                              # WPF 视图
│   ├─ MainWindow.xaml
│   ├─ SinglePeriodCompareView.xaml
│   ├─ MultiStockCompareView.xaml
│   ├─ MetricsView.xaml
│   └─ TrendChartView.xaml
└─ Converters/Converters.cs            # 显示用 IValueConverter
```

依赖：
- [Newtonsoft.Json](https://www.newtonsoft.com/json) — 解析 JSON
- [CommunityToolkit.Mvvm](https://learn.microsoft.com/dotnet/communitytoolkit/mvvm/) — MVVM 基础
- [Microsoft.Data.Sqlite](https://learn.microsoft.com/dotnet/standard/data/sqlite/) — 本地缓存
- [ClosedXML](https://github.com/ClosedXML/ClosedXML) — Excel 导出
- [OxyPlot.Wpf](https://oxyplot.github.io/) — 图表绘制

---

## 5. 使用到的东方财富接口

| 用途       | URL 模板 |
|------------|----------|
| 股票联想搜索 | `https://searchadapter.eastmoney.com/api/suggest/get?input={kw}&type=14&count=20` |
| 三大报表    | `https://datacenter.eastmoney.com/securities/api/data/v1/get?reportName=RPT_F10_FINANCE_GBALANCE\|GINCOME\|GCASHFLOW&columns=ALL&filter=(SECUCODE="600000.SH")&pageNumber=1&pageSize=20&sortColumns=REPORT_DATE&sortTypes=-1` |

> 字段映射集中在 `EastMoneyService.cs` 的三个 `*FieldMap`，
> 如东方财富调整字段，仅修改这些字典即可。

---

## 6. 扩展建议

- **指标计算**：在 `MetricsService` 上叠加“营运资本变化、自由现金流、现金转换周期”等。
- **数据源**：可以通过实现一个 `IFinancialDataSource` 抽象，接入 Tushare / Choice 等付费数据源。
- **国际化**：把字段映射拆为多个 JSON 资源文件，按公司类型（一般/银行/证券/保险）切换。

---

## 7. 免责声明

- 数据来自第三方公开接口，可能存在延迟、缺失或不准确，仅供学习与研究使用，
  不构成任何投资建议。
- 接口为东方财富所有，请遵守其使用条款，不要进行高频抓取。
