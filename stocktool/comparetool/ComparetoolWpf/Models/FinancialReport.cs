namespace ComparetoolWpf.Models;

/// <summary>
/// 报表种类。
/// </summary>
public enum ReportKind
{
    /// <summary>资产负债表</summary>
    Balance,
    /// <summary>利润表</summary>
    Income,
    /// <summary>现金流量表</summary>
    CashFlow,
}

/// <summary>
/// 报告期类型（年报/中报/季报）。
/// </summary>
public enum ReportPeriodType
{
    /// <summary>全部</summary>
    All = 0,
    /// <summary>年报（截止 12-31）</summary>
    Annual = 1,
    /// <summary>中报（截止 06-30）</summary>
    SemiAnnual = 2,
    /// <summary>一季报（截止 03-31）</summary>
    Q1 = 3,
    /// <summary>三季报（截止 09-30）</summary>
    Q3 = 4,
}

/// <summary>
/// 单期财务报表：包含报告期 + 若干指标 (中文名称 -> 数值)。
/// 数值单位通常为 元（东方财富原始返回）。
/// </summary>
public class FinancialReport
{
    /// <summary>报告期，例如 2024-12-31。</summary>
    public DateTime ReportDate { get; set; }

    /// <summary>报告期显示文本，如 <c>2024年报</c> / <c>2024中报</c>。</summary>
    public string PeriodLabel { get; set; } = string.Empty;

    /// <summary>报表种类。</summary>
    public ReportKind Kind { get; set; }

    /// <summary>所属股票代码（带市场前缀，如 SH600000）。</summary>
    public string StockFullCode { get; set; } = string.Empty;

    /// <summary>指标 中文名 -> 数值（可空，因不同公司部分指标缺失）。</summary>
    public Dictionary<string, double?> Items { get; set; } = new();
}
