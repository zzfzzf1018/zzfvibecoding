namespace StockAnalyzer.Core.Models;

/// <summary>
/// 定期报告中的每股财务指标（累计口径）。
/// </summary>
public sealed class FinancialReport
{
    public string Code { get; set; } = string.Empty;

    public MarketType Market { get; set; }

    /// <summary>报告期，如 2024-09-30。</summary>
    public DateTime ReportDate { get; set; }

    /// <summary>公告日期。用于做“时点可见”对齐，避免未来函数。</summary>
    public DateTime NoticeDate { get; set; }

    /// <summary>基本每股收益（年初至报告期累计，元/股）。</summary>
    public double? BasicEps { get; set; }

    /// <summary>数据源直接提供的滚动每股收益（TTM）；为空时由累计口径自行还原。</summary>
    public double? EpsTtm { get; set; }

    /// <summary>每股净资产（时点值，元/股）。</summary>
    public double? BookValuePerShare { get; set; }

    /// <summary>归母净利润（累计）。</summary>
    public double? NetProfit { get; set; }

    /// <summary>加权净资产收益率（%，累计）。</summary>
    public double? WeightedRoe { get; set; }

    /// <summary>报告期所在季度（1-4）。</summary>
    public int Quarter => (ReportDate.Month - 1) / 3 + 1;

    public int Year => ReportDate.Year;
}
