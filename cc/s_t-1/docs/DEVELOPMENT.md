# 开发指南

## 1. 环境准备

| 组件 | 版本 | 说明 |
|------|------|------|
| Windows | 10 1809+ | WPF 目标平台 |
| .NET SDK | 8.0.x | `dotnet --version` 校验 |
| IDE | Visual Studio 2022 17.8+ / Rider / VS Code + C# Dev Kit | 需要「.NET 桌面开发」工作负载 |

```powershell
git clone <repo>
cd s_t-1
dotnet restore
```

---

## 2. 常用命令

```powershell
# 一键：还原 + 编译 + 测试
./build/build.ps1

# 只编译 Debug，不跑测试
./build/build.ps1 -Configuration Debug -SkipTests

# 运行桌面程序
dotnet run --project src/StockAnalyzer.Desktop

# 只跑测试
dotnet test

# 跑单个测试类
dotnet test --filter "FullyQualifiedName~ValuationAnalyzerTests"

# 发布
./build/publish.ps1                          # 依赖框架
./build/publish.ps1 -SelfContained -SingleFile  # 自包含单文件

# 清理
./build/clean.ps1
```

若 PowerShell 提示脚本被策略阻止，用 `build/build.cmd` / `build/publish.cmd`，
它们内部已带 `-ExecutionPolicy Bypass`。

---

## 3. 第三方依赖

| 包 | 用途 |
|----|------|
| `Microsoft.EntityFrameworkCore.Sqlite` | 本地持久化 |
| `Microsoft.Extensions.Hosting` / `Http` / `Options` | DI、配置、HttpClientFactory |
| `CommunityToolkit.Mvvm` | MVVM 源生成器 |
| `ScottPlot.WPF` | 图表 |
| `System.Text.Encoding.CodePages` | GB2312 编码（拼音首字母） |
| `xunit` | 单元测试 |

---

## 4. 编码约定

- `Nullable` 与 `ImplicitUsings` 全解决方案启用（见 `Directory.Build.props`）。
- 文件作用域命名空间；4 空格缩进；XAML / json / csproj 用 2 空格（见 `.editorconfig`）。
- **注释只写代码本身表达不出来的信息**——例如接口字段口径、限流策略、算法假设，
  不要复述代码在做什么。
- 领域算法一律放在 `Core/Analytics`，保持**纯函数、无 IO**，便于单测。
- 数据源实现里对返回值要**容错**：东财常用 `"-"` 表示缺失，
  统一走 `JsonValueExtensions` 的 `GetDoubleOrNull` / `NullIfZero`。
- 网络失败**返回空集合而非抛异常**，由上层决定降级策略。

---

## 5. 调试技巧

### 观察数据源请求

日志走 `ILogger`，Debug 配置下输出到 VS 的「输出 → 调试」窗口。
把 `appsettings.json` 中的日志级别调低即可看到更多细节：

```jsonc
{ "Logging": { "LogLevel": { "Default": "Debug" } } }
```

### 直接查看本地数据库

用任意 SQLite 客户端（DB Browser for SQLite 等）打开
`%LOCALAPPDATA%\StockAnalyzer\stock.db`：

```sql
SELECT COUNT(*) FROM Stocks;
SELECT * FROM Watchlist;
SELECT * FROM DailyBars WHERE Code='600519' ORDER BY Date DESC LIMIT 10;
SELECT * FROM FinancialReports WHERE Code='600519' ORDER BY ReportDate DESC LIMIT 8;
SELECT * FROM SyncStamps;
```

### 重置到干净状态

```powershell
./build/clean.ps1 -IncludeLocalData
```

### 单独验证某个接口

新建一个临时控制台项目，引用 `StockAnalyzer.DataSource`，直接调用
`IStockDataSource` 的方法即可（无需启动 WPF）：

```csharp
var services = new ServiceCollection();
services.AddLogging(b => b.AddConsole());
services.AddEastmoneyDataSource();
var source = services.BuildServiceProvider().GetRequiredService<IStockDataSource>();

var hits = await source.SearchAsync("茅台", MarketGroup.All, 10);
```

---

## 6. 修改数据库结构

当前使用 `EnsureCreatedAsync()`（无迁移），适合本地缓存型数据库。
**新增字段后老库不会自动升级**，处理方式二选一：

- 开发期：删掉 `stock.db` 重建（数据都能重新抓取，只有自选股会丢）；
- 正式化：改为 EF Core Migrations——

  ```powershell
  dotnet tool install --global dotnet-ef
  dotnet ef migrations add <Name> -p src/StockAnalyzer.Data -s src/StockAnalyzer.Desktop
  ```

  并把 `InitializeAsync` 里的 `EnsureCreatedAsync()` 换成 `MigrateAsync()`。

---

## 7. 扩展方向

| 需求 | 落点 |
|------|------|
| 新增估值指标（PS、股息率、PEG） | `ValuationMetric` 枚举 + `ValuationPoint.GetMetric/GetAnchor` + 数据源补字段 |
| 指数 / ETF 估值 | 扩展 `MarketType` 与检索过滤条件；指数 PE 需要成分股加权，需新增数据源方法 |
| 自选股分组、备注 | `WatchlistItem` 已预留 `Note` / `SortOrder`，仓储方法也已具备，只差界面 |
| 导出 CSV / Excel | 在 `StockDetailViewModel` 增加命令，直接序列化 `ValuationSeries` |
| 估值预警 | 新增后台服务定时比对分位数阈值并发通知 |
| 更换数据源 | 见 [DATA_SOURCE.md](DATA_SOURCE.md) 第 5 节 |

---

## 8. 常见问题

**编译报 `CA1416` 平台兼容性警告？**
已在 `Directory.Build.props` 中通过 `NoWarn` 抑制；WPF 项目本身只面向 Windows。

**ScottPlot 图表中文显示为方块？**
不要用 `plot.Font.Set("Microsoft YaHei")`——SkiaSharp 找不到该字体时会**静默回退**到不含中文字形的
字体，反而把已经选对的字体覆盖掉。正确做法见 `ChartStyler.ApplyChineseFont`：先调
`plot.Font.Automatic()` 让 ScottPlot 根据实际字符自动探测，再用 `Fonts.Detect(...)` 的结果单独
补上标题与坐标轴标签（这两处不在 `Automatic()` 的覆盖范围内）。
**添加完所有图元之后**才能调用这个方法。

**修改 build/*.ps1 后报语法错误？**
Windows PowerShell 5.1 对无 BOM 的 UTF-8 脚本按 ANSI 解析，中文会乱码并导致解析失败。
本仓库的 `.ps1` 均以 **UTF-8 with BOM** 保存，编辑时请保持。

**发布后启动报缺少运行时？**
用 `./build/publish.ps1 -SelfContained -SingleFile` 发布自包含版本。
注意 WPF **不支持裁剪**，脚本已固定 `PublishTrimmed=false`。

**测试里出现日期相关的偶发失败？**
分位数计算依赖「最新交易日」。单元测试统一显式传入 `asOf` 参数固定时点，
新增测试时请沿用这个做法，不要依赖 `DateTime.Today`。
