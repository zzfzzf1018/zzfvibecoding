# StockTechAnalyzer — A股技术分析工具 (.NET 8 WinForms)

## 功能

- 股票搜索（代码 / 拼音 / 名称，沪深京全市场）
- K 线图（日 / 周 / 月）+ 均线 MA5/10/20/60 + BOLL 通道
- 成交量副图（红涨绿跌）
- MACD (12,26,9) — DIF / DEA / 柱
- KDJ (9,3,3) + RSI(14)（共面叠加）
- 筹码分布估算（三角分布 + 衰减换手）：均成本、获利盘比例、70% 集中度
- 综合情绪评分（−100 ~ +100）：趋势 / MACD / KDJ / RSI / BOLL / 量能 / 筹码加权
- 操作建议文字输出
- 自选股本地保存（`%AppData%\StockTechAnalyzer\settings.json`）
- 数据源可切换：**新浪财经**（默认，免费）/ **Tushare Pro**（需在 设置 中填 Token）

## 运行

```powershell
cd StockTechAnalyzer
dotnet run -c Release
```

首次启动后：
1. 在搜索框输入 `平安银行` 或 `000001`，回车
2. 双击搜索结果加载
3. 顶部切换 日/周/月 周期，调整 K 线数量后点 **刷新**
4. 切换到 **筹码分布** 标签查看持仓成本结构
5. 右侧 **综合分析** 面板查看评分、信号、操作建议
6. **+** 按钮加入自选，**−** 按钮移除

## 技术栈

- .NET 8 Windows Forms
- [ScottPlot 5](https://scottplot.net/) WinForms 控件
- 自实现：技术指标、筹码分布、情绪评分

## 数据源说明

- **新浪**：`hq.sinajs.cn` / `quotes.sina.cn` 公开端点，免费、无需 Key。周/月线由日线自动聚合。
- **Tushare**：`http://api.waditu.com` REST。在 *设置* 中填入 Token；搜索/实时报价仍走新浪。

## 风险提示

本工具仅供学习与研究用途，所有指标与情绪评分均为算法估算，**不构成任何投资建议**。投资有风险，入市需谨慎。
