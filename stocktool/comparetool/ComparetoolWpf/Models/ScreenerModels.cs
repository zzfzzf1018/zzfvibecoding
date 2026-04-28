namespace ComparetoolWpf.Models;

/// <summary>
/// 选股器筛选条件（所有字段为 null 表示不限）。
/// 数值单位：百分比项以小数表示（0.1 = 10%）。
/// </summary>
public class ScreenerFilter
{
    /// <summary>报告期，必填，例如 "2024-12-31"。</summary>
    public string ReportDate { get; set; } = string.Empty;

    /// <summary>行业关键字（按行业名称模糊匹配）。</summary>
    public string? IndustryContains { get; set; }

    /// <summary>ROE（加权）下限。</summary>
    public double? RoeMin { get; set; }
    /// <summary>毛利率下限。</summary>
    public double? GrossMarginMin { get; set; }
    /// <summary>净利率下限（净利润/营收）。</summary>
    public double? NetMarginMin { get; set; }
    /// <summary>营业总收入同比下限。</summary>
    public double? RevenueYoYMin { get; set; }
    /// <summary>归母净利润同比下限。</summary>
    public double? NetProfitYoYMin { get; set; }
    /// <summary>EPS 基本每股收益下限（元）。</summary>
    public double? EpsMin { get; set; }

    /// <summary>每页返回数量。</summary>
    public int PageSize { get; set; } = 200;
}

/// <summary>选股结果一行。</summary>
public class ScreenerRow
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Industry { get; set; } = string.Empty;

    public double? Eps { get; set; }
    public double? Revenue { get; set; }
    public double? NetProfit { get; set; }
    public double? Roe { get; set; }
    public double? GrossMargin { get; set; }
    public double? NetMargin { get; set; }
    public double? RevenueYoY { get; set; }
    public double? NetProfitYoY { get; set; }
}
