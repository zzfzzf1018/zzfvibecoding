# A 股财报对比工具 (ComparetoolWpf)

一个基于 **C# / WPF (.NET 8)** 的桌面应用，用于查询 A 股上市公司的三大财务报表，
并提供两类核心分析能力：

1. **单股期间对比**：选定一只股票，加载其多期（年报 / 中报 / 季报）报表，
   选择任意两期进行差异比较，自动高亮变化幅度超过阈值的指标。
2. **多股横向对比（百分比报表）**：选定多只股票，加载同一报告期的同一报表，
   将每个指标按“基准项”（资产总计 / 营业总收入 / 经营活动现金流入小计）
   折算成百分比，便于跨公司同口径比较。

> 数据来源：东方财富公开 F10 接口，无需注册或 API Key。

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

### 3.1 单股期间对比

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

### 3.2 多股横向对比（百分比报表）

1. 搜索 → 选中股票 → 点击 **加入对比池**，重复以加入多只股票。
2. 选择 **报表类型** 与 **报告期类型**，点击 **加载并对比**。
3. 工具会拉取每只股票“最近一期”同类型报表，对每个指标计算占基准项的比例：

| 报表类型     | 基准项           |
|--------------|------------------|
| 资产负债表   | 资产总计         |
| 利润表       | 营业总收入       |
| 现金流量表   | 经营活动现金流入小计 |

每只股票生成两列：原始数值 + 百分比，可直接横向比较结构差异
（如毛利率、费用率、资产构成等）。

---

## 4. 项目结构

```
ComparetoolWpf/
├─ App.xaml / App.xaml.cs              # 应用启动
├─ Models/                             # 数据模型
│   ├─ StockInfo.cs                    # 股票基本信息
│   ├─ FinancialReport.cs              # 单期报表
│   └─ ComparisonRow.cs                # 对比结果行
├─ Services/
│   ├─ EastMoneyService.cs             # 东方财富数据访问
│   └─ ComparisonService.cs            # 期间对比 / 同口径对比 计算
├─ ViewModels/                         # MVVM 视图模型
│   ├─ MainViewModel.cs
│   ├─ SinglePeriodCompareViewModel.cs
│   └─ MultiStockCompareViewModel.cs
├─ Views/                              # WPF 视图
│   ├─ MainWindow.xaml
│   ├─ SinglePeriodCompareView.xaml
│   └─ MultiStockCompareView.xaml
└─ Converters/Converters.cs            # 显示用 IValueConverter
```

依赖：
- [Newtonsoft.Json](https://www.newtonsoft.com/json) — 解析 JSON
- [CommunityToolkit.Mvvm](https://learn.microsoft.com/dotnet/communitytoolkit/mvvm/) — MVVM 基础

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

- **导出**：基于 `DataGrid` 内容导出 CSV / Excel（可引入 `EPPlus` / `ClosedXML`）。
- **指标计算**：在 `ComparisonService` 之上叠加“同比 / 环比 / 杜邦分析 / ROE 拆解”等。
- **缓存**：把已下载的报表缓存到本地 SQLite（`Microsoft.Data.Sqlite`），减少接口压力。
- **绘图**：使用 `OxyPlot` 或 `LiveCharts2` 展示趋势曲线。
- **数据源**：可以通过实现一个 `IFinancialDataSource` 抽象，接入 Tushare / Choice 等付费数据源。

---

## 7. 免责声明

- 数据来自第三方公开接口，可能存在延迟、缺失或不准确，仅供学习与研究使用，
  不构成任何投资建议。
- 接口为东方财富所有，请遵守其使用条款，不要进行高频抓取。
