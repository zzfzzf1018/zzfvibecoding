# 估值计算工具箱（Valuation Toolkit）

一款基于 C# / WPF 的 Windows 桌面估值计算软件，内置 **11 个专业估值模型**，覆盖绝对估值、相对估值、折现率与增长率测算、现金流与衍生品定价四大类。所有参数（折现率、增长率、安全边际等）均可实时调整，输入即算，并自动生成测算明细与双因素敏感性分析表。

- 零第三方运行时依赖，纯 WPF 实现
- 算法与界面完全分离，核心库可独立复用
- 60 个自动化测试覆盖金融算法与界面渲染

---

## 目录

- [功能总览](#功能总览)
- [界面说明](#界面说明)
- [快速开始](#快速开始)
- [输入约定](#输入约定)
- [模型详解](#模型详解)
- [项目结构](#项目结构)
- [架构设计](#架构设计)
- [扩展指南：新增一个估值工具](#扩展指南新增一个估值工具)
- [测试](#测试)
- [已知限制](#已知限制)
- [免责声明](#免责声明)

---

## 功能总览

| 分类 | 工具 | 核心用途 | 敏感性分析维度 |
|---|---|---|---|
| 绝对估值 | **DCF 现金流折现** | 两阶段自由现金流折现，企业价值与每股内在价值 | 折现率 × 永续增长率 |
| 绝对估值 | **DDM 股利折现** | 两阶段股利折现，适用高分红成熟企业 | 股权成本 × 永续增长率 |
| 绝对估值 | **剩余收益模型 RIM** | 账面净资产 + 未来超额收益现值 | ROE × 股权成本 |
| 相对估值 | **PEG 市盈率相对增长** | 市盈率与盈利增速匹配度、合理股价与目标价 | 增长率 × 目标 PEG |
| 相对估值 | **可比公司倍数法** | PE / PB / PS / P·CF / EV·EBITDA 五法交叉验证 | —（多方法对比表） |
| 相对估值 | **格雷厄姆内在价值** | 经典内在价值公式、格雷厄姆数、NCAV 清算价值 | — |
| 折现率与增长率 | **折现率 WACC / CAPM** | CAPM 求股权成本并加权得 WACC，含去杠杆 Beta | 市场风险溢价 × Beta |
| 折现率与增长率 | **增长率测算** | CAGR、可持续增长率、基本面增长率、市场隐含增长率 | — |
| 现金流工具 | **NPV / IRR 项目评估** | NPV、IRR、MIRR、获利指数、静态与折现回收期 | 现金流调整 × 折现率 |
| 现金流工具 | **债券估值** | 债券定价、YTM 反推、麦考利久期、修正久期、凸性 | 收益率变动对价格的影响 |
| 现金流工具 | **期权 / 实物期权定价** | Black-Scholes-Merton 定价与全套希腊字母 | 波动率 × 标的价格 |

每个工具都提供：

- **实时计算**：任一输入变化立即重算，无需点击按钮
- **风险提示**：自动识别不合理假设（终值占比过高、折现率低于永续增长率、ROE 低于股权成本等）
- **报告导出**：一键复制到剪贴板或导出为含完整输入假设、结果与表格的 txt 报告
- **恢复默认**：一键还原到内置的示例参数

---

## 界面说明

```
┌──────────────┬────────────────────────────────────────────────────────┐
│              │  标题 / 说明 / 模型公式    [恢复默认][复制报告][导出报告] │
│  搜索框      ├───────────────────┬────────────────────────────────────┤
│              │                   │  ⚠ 风险提示                        │
│  绝对估值    │   输入参数         │                                    │
│   · DCF      │   （分组卡片）     │  计算结果（主结果高亮显示）         │
│   · DDM      │                   │                                    │
│   · RIM      │   现金流预测       │  测算明细（逐年现金流 / 折现表）    │
│  相对估值    │   折现率与终值     │                                    │
│   · PEG      │   股权价值换算     │  敏感性分析（双因素 5×5 矩阵）      │
│   · ...      │                   │                                    │
└──────────────┴───────────────────┴────────────────────────────────────┘
```

左侧导航按分类分组，支持按工具名称、分类或说明关键词搜索。

---

## 快速开始

### 环境要求

| 用途 | 要求 |
|---|---|
| 运行 | Windows 10/11 + [.NET 8 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/8.0) |
| 开发 | Windows + [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) 或更高 |

### 构建与运行

```powershell
# 还原并构建
dotnet build

# 运行桌面应用
dotnet run --project src\ValuationTools.Desktop

# 运行全部测试
dotnet test
```

### 发布

```powershell
# 标准发布（推荐，离线可用，不需要额外拉取 NuGet 包）
dotnet publish src\ValuationTools.Desktop -c Release
```

产物位于 `src\ValuationTools.Desktop\bin\Release\net8.0-windows\publish\`，双击其中的 `ValuationTools.exe` 即可运行（目标机需已安装 .NET 8 Desktop Runtime）。

如需打包成单个文件，可指定运行时标识：

```powershell
# 单文件（依赖框架，体积小）
dotnet publish src\ValuationTools.Desktop -c Release -r win-x64 `
  --self-contained false -p:PublishSingleFile=true

# 单文件（自包含，目标机无需安装运行时，体积约 150 MB）
dotnet publish src\ValuationTools.Desktop -c Release -r win-x64 `
  --self-contained true -p:PublishSingleFile=true
```

> 指定 `-r` 会触发运行时相关包的还原，需要能访问 nuget.org。若处于离线或内网受限环境，请使用上面的标准发布。

---

## 输入约定

| 约定 | 说明 |
|---|---|
| **百分比字段** | 界面直接填数字，`9` 表示 9%。带 `%` 后缀的字段一律如此 |
| **金额单位** | 同一工具内保持一致即可。示例参数以「万元」为总量单位、「元」为每股单位 |
| **股本单位** | 与金额单位配套。若金额用万元，则股本用万股，得到的每股价值即为「元」 |
| **净债务** | 有息负债 − 现金。折现 FCFE（股权自由现金流）时应填 0，因为已隐含扣除 |
| **安全边际** | 用于反推建议买入价：`买入价 = 内在价值 × (1 − 安全边际)` |
| **留空 / 填 0** | 部分字段填 0 表示「不使用」，例如债券的市场价格填 0 表示按给定 YTM 定价 |

> 输入非数字内容时输入框会变红并保留上一个有效值，不会导致计算中断。

---

## 模型详解

### 1. DCF 现金流折现

$$EV = \sum_{t=1}^{n} \frac{FCF_t}{(1+WACC)^t} + \frac{TV}{(1+WACC)^n},\quad \text{股权价值} = EV - \text{净债务}$$

- **两阶段增长**：可分别设定第一/第二阶段的年数与增长率，模拟高速增长后的换挡
- **两种终值方法**：
  - 永续增长法（戈登）：$TV = \dfrac{FCF_n \times (1+g)}{WACC - g}$
  - 退出倍数法：$TV = \text{终期指标} \times \text{退出倍数}$（终期指标留空则用末年现金流）
- **期中折现法**：勾选后按 $t - 0.5$ 折现，假设现金流在年内均匀发生，估值略高于年末折现
- **隐含永续增长率反推**：由当前股价二分求解市场隐含的长期增速，用于判断市场预期是否过高
- **风险提示**：终值现值占企业价值超过 80% 时告警——此时估值几乎完全由无法验证的长期假设决定

**适用**：现金流可预测的成熟企业。**不适用**：早期亏损公司、金融机构（用 DDM 或 RIM）。

### 2. DDM 股利折现

$$P_0 = \sum_{t=1}^{n} \frac{D_t}{(1+R_e)^t} + \frac{D_{n+1}}{(R_e-g)(1+R_e)^n}$$

- 支持**增长率线性衰减**：从高增长率逐年平滑过渡到永续增长率，比突变式两阶段更贴近现实
- **隐含预期回报率**：按当前股价反推买入并长期持有的年化回报（即使 IRR）
- 高增长期年数填 0 时退化为单阶段戈登模型

**适用**：分红稳定、派息政策清晰的银行、公用事业、消费龙头。**不适用**：不分红公司。

### 3. 剩余收益模型 RIM

$$V_0 = B_0 + \sum_{t=1}^{n} \frac{(ROE - R_e) \times B_{t-1}}{(1+R_e)^t} + PV(TV)$$

- 价值 = 账面净资产 + 未来超额收益现值，其中净资产按留存收益逐年滚存：$B_t = B_{t-1}(1 + ROE \times (1-\text{分红率}))$
- **持续性因子 ω**：刻画超额收益的衰减速度，终值 $TV = \dfrac{RI_n \times \omega}{1 + R_e - \omega}$
  - ω = 0：竞争优势立即消失
  - ω = 1：护城河永续保持
  - 实务常取 0.4 ~ 0.8
- 输出**隐含合理 PB**，便于与市场 PB 直接比较

**适用**：不分红、现金流波动大但盈利稳定的公司，估值对终值假设的依赖远低于 DCF。

### 4. PEG 市盈率相对增长

$$PEG = \frac{PE}{g\%},\quad \text{合理 } PE = g\% \times \text{目标 PEG}$$

- **PEGY**：$PE \div (g\% + \text{股息率}\%)$，把分红纳入考量，更适合中速增长的分红股
- **隐含增长率**：当前 PE 对应的市场预期增速
- **持有期目标价与年化回报**：按持有年限推算未来 EPS 并给予合理 PE，叠加股息得到预期年化收益

**注意**：增长率为负时 PEG 失效；增速超过 50% 时 PEG 会系统性高估合理价值，需用 DCF 交叉验证。

### 5. 可比公司倍数法

同时计算 5 组倍数的隐含股价并取平均值与中位数：

| 方法 | 隐含股价 |
|---|---|
| PE / PB / PS / P·CF | 可比倍数 × 对应每股指标 |
| EV/EBITDA | (可比倍数 × EBITDA − 净债务) ÷ 总股本 |

当各方法结果差异超过 3 倍时会告警，提示可比公司选择或财务口径可能不一致。

### 6. 格雷厄姆内在价值

$$V = \frac{EPS \times (8.5 + 2g) \times 4.4}{Y},\quad \text{格雷厄姆数} = \sqrt{22.5 \times EPS \times BVPS}$$

三重安全边际检验：修正公式内在价值、格雷厄姆数（防御型投资者价格上限）、NCAV 的 2/3（烟蒂股买入线）。

**注意**：公式中增长率是线性放大 PE 的，增速超过 15% 时结果失真严重。

### 7. 折现率 WACC / CAPM

$$R_e = R_f + \beta \times MRP + \text{规模溢价} + \text{国家风险溢价}$$
$$WACC = R_e \times \frac{E}{D+E} + R_d(1-T) \times \frac{D}{D+E}$$

额外输出 **Hamada 去杠杆 Beta**：$\beta_U = \dfrac{\beta_L}{1 + (1-T) \times D/E}$，用于把可比公司的 Beta 调整到目标公司的资本结构。

### 8. 增长率测算

为 DCF / PEG 提供有依据的增长假设，而非拍脑袋：

| 指标 | 公式 | 含义 |
|---|---|---|
| 历史 CAGR | $(\text{期末}/\text{期初})^{1/n} - 1$ | 历史实际增速 |
| 可持续增长率 | $ROE \times (1 - \text{分红率})$ | 不依赖外部融资的增速上限 |
| 基本面增长率 | 再投资率 × ROIC | 由资本效率驱动的内生增速 |
| PE 隐含增长率 | $r - \text{分红率} / PE$ | 市场当前定价隐含的长期增速 |

### 9. NPV / IRR 项目评估

支持最多 10 年的任意现金流序列，输出 NPV、IRR（二分法求解）、MIRR、获利指数 PI、静态回收期与折现回收期，并附现金流调整幅度 × 折现率的双因素 NPV 敏感性表。

### 10. 债券估值

- 填写 YTM → 计算理论价格；填写市场价格 → 反推 YTM
- 输出麦考利久期、修正久期、凸性，以及**利率上行 1% 的价格变动估计**（含凸性修正）：

$$\frac{\Delta P}{P} \approx -D_{mod} \times \Delta y + \frac{1}{2} \times C \times (\Delta y)^2$$

### 11. 期权 / 实物期权定价

Black-Scholes-Merton 模型，输出看涨/看跌价格、内在价值与时间价值拆分、行权概率 N(d₂)，以及 Delta、Gamma、Vega、Theta、Rho 全套希腊字母。可用于股票期权、可转债期权价值，以及把「延迟投资权」作为实物期权定价的项目评估。

---

## 项目结构

```
eval/
├── ValuationTools.sln
├── .gitignore
├── README.md
├── src/
│   ├── ValuationTools.Core/                 # 纯算法库（net8.0，无 UI 依赖）
│   │   ├── Common/
│   │   │   └── FinancialMath.cs             # 折现、NPV、IRR、CAGR、正态分布
│   │   └── Calculators/
│   │       ├── DcfCalculator.cs
│   │       ├── DdmCalculator.cs
│   │       ├── ResidualIncomeCalculator.cs
│   │       ├── PegCalculator.cs
│   │       ├── RelativeValuationCalculator.cs
│   │       ├── GrahamCalculator.cs
│   │       ├── DiscountRateCalculator.cs
│   │       ├── GrowthCalculator.cs
│   │       ├── ProjectCashFlowCalculator.cs
│   │       ├── BondCalculator.cs
│   │       └── OptionCalculator.cs
│   └── ValuationTools.Desktop/              # WPF 界面（net8.0-windows）
│       ├── App.xaml(.cs)
│       ├── MainWindow.xaml(.cs)
│       ├── Themes/Styles.xaml               # 配色、控件模板
│       ├── Infrastructure/                  # ObservableObject、RelayCommand、转换器
│       ├── Models/InputField.cs             # NumberField / ChoiceField / ToggleField
│       ├── ViewModels/
│       │   ├── ToolViewModel.cs             # 工具基类：输入管理、自动重算、报告导出
│       │   ├── MainViewModel.cs             # 工具列表、分组、搜索
│       │   └── Tools/                       # 11 个工具的视图模型
│       └── Views/ToolView.xaml(.cs)         # 通用工具界面（所有工具共用）
└── tests/
    └── ValuationTools.Core.Tests/
        ├── CalculatorTests.cs               # 金融算法正确性
        ├── ToolViewModelTests.cs            # 视图模型行为
        ├── ToolViewRenderingTests.cs        # 真实 WPF 渲染验证
        └── WpfFixture.cs                    # 常驻 STA 线程 + Application
```

---

## 架构设计

### 分层

```
ValuationTools.Core（算法）  ←  ValuationTools.Desktop（界面）
     纯函数、无状态                  MVVM，无业务计算
```

核心库不引用任何 UI 类型，可直接被 Web API、控制台或 Excel 插件复用。

### 关键设计：一套界面驱动所有工具

传统做法是为每个工具写一个 XAML 页面，11 个工具就是 11 份高度重复的界面代码。本项目改为**数据驱动**：

- 每个工具的 ViewModel 只声明**输入项集合**（`NumberField` / `ChoiceField` / `ToggleField`）和**结果集合**
- [ToolView.xaml](src/ValuationTools.Desktop/Views/ToolView.xaml) 通过 `DataType` 隐式模板自动渲染任意输入项组合
- 新增工具无需编写任何 XAML

### 自动重算

`ToolViewModel` 在构造末尾调用 `Ready()`，此后任一输入项的 `PropertyChanged` 都会触发 `Recalculate()`：清空上次结果 → 调用 `Compute()` → 捕获异常并转为界面提示。计算异常不会导致崩溃，也不会残留过期结果。

### 表格列名的坑

WPF 绑定路径中 `.` 是层级分隔符。如果把 `8.00%` 这类格式化文本直接作为 `DataTable` 列名，自动生成的列会全部绑定失败、显示空白。因此 `ToolViewModel.CreateTable()` 统一使用 `C0/C1/…` 作为列名，真实表头存入 `DataColumn.Caption`，再由 `ToolView.OnAutoGeneratingColumn` 还原显示。[ToolViewRenderingTests](tests/ValuationTools.Core.Tests/ToolViewRenderingTests.cs) 会在真实窗口中逐格校验，防止回归。

---

## 扩展指南：新增一个估值工具

以新增「EV/EBIT 估值」为例，三步即可：

**第一步：在核心库写算法**

```csharp
// src/ValuationTools.Core/Calculators/MyCalculator.cs
public sealed class MyInput
{
    public double Ebit { get; init; }
    public double Multiple { get; init; }
}

public sealed class MyResult
{
    public double EnterpriseValue { get; init; }
    public string? Warning { get; init; }
}

public static class MyCalculator
{
    public static MyResult Calculate(MyInput input) => new()
    {
        EnterpriseValue = input.Ebit * input.Multiple
    };
}
```

**第二步：写 ViewModel**

```csharp
// src/ValuationTools.Desktop/ViewModels/Tools/MyViewModel.cs
public sealed class MyViewModel : ToolViewModel
{
    public MyViewModel() : base("EV/EBIT 估值", "相对估值", "用息税前利润倍数估算企业价值。")
    {
        Formula = "EV = EBIT × 倍数";

        AddGroup("经营数据",
            Number("ebit", "息税前利润 EBIT", 5000, " 万元"),
            Number("multiple", "EV/EBIT 倍数", 12, "x"));

        Ready();   // 必须在最后调用，触发首次计算
    }

    protected override void Compute()
    {
        var result = MyCalculator.Calculate(new MyInput
        {
            Ebit = V("ebit"),          // V=数值  R=百分比转小数  I=整数
            Multiple = V("multiple")   // B=开关  Selected=下拉索引
        });

        AddResult("企业价值", Money0(result.EnterpriseValue) + " 万元", isPrimary: true);
        SetNotice(result.Warning);
    }
}
```

**第三步：注册到导航**

在 [MainViewModel](src/ValuationTools.Desktop/ViewModels/MainViewModel.cs) 的 `Tools` 集合中加入 `new MyViewModel()`。分类相同的工具会自动归入同一导航分组。

若需要明细表或敏感性表，在 `Compute()` 中调用：

```csharp
var table = CreateTable("年份", "现金流", "现值");   // 务必用 CreateTable，勿直接 new DataTable
table.Rows.Add("第 1 年", "1,000", "917");
SetSchedule(table);        // 或 SetSensitivity(table)
ScheduleTitle = "测算明细";
```

---

## 测试

```powershell
dotnet test
```

60 个测试分三层：

| 测试文件 | 覆盖内容 |
|---|---|
| `CalculatorTests` | 金融算法正确性：DCF 闭式解校验、IRR 使 NPV 归零、期权 Put-Call 平价、债券平价定价、RIM 在 ROE=Re 时等于账面价值、WACC 权重计算等 |
| `ToolViewModelTests` | 界面行为：所有工具默认参数可算出结果、输入变化触发重算、恢复默认、非法输入转为错误提示且清空过期结果、报告导出格式、文件名合法性 |
| `ToolViewRenderingTests` | 真实渲染：在常驻 STA 线程中加载 `Application` 与产品 `ToolView`，遍历可视树逐格核对表头与单元格文本 |

第三层是关键——纯 ViewModel 测试无法发现绑定层面的问题（数据正确但界面空白），只有真实渲染才能暴露。

---

## 已知限制

- **仅支持 Windows**：WPF 依赖 Windows 平台，核心算法库本身跨平台
- **单次计算，不保存**：目前不持久化用户输入，重启后回到默认参数；可通过「导出报告」留存结果
- **预测期上限**：DCF / DDM / RIM 最多 100 年，债券最多 1200 期，防止误输入导致界面卡死
- **NPV/IRR 现金流年数上限 10 年**
- **IRR 多解情形**：现金流符号多次变化时可能存在多个 IRR，本工具返回二分法找到的其中一个，此时应以 MIRR 或 NPV 为准
- **不含行情数据**：所有参数需手工输入，不联网获取财务数据

---

## 免责声明

本软件是估值**计算工具**，不是投资建议工具。

所有输出完全取决于使用者输入的假设——折现率、增长率、可比倍数的微小变化都会显著改变结果，这正是软件内置敏感性分析的原因。估值模型能帮助厘清逻辑，但无法替代对商业模式、竞争格局与管理层的判断。

计算结果仅供研究与学习参考，据此进行的任何投资决策及其后果由使用者自行承担。
