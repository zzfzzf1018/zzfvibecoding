namespace ComparetoolWpf.Models;

/// <summary>
/// 选股器筛选条件（所有字段为 null 表示不限）。
/// 数值单位：百分比项以小数表示（0.1 = 10%）。
/// </summary>
public class ScreenerFilter
{
    /// <summary>报告期，必填，例如 "2024-12-31"。必须是季报日（3-31/6-30/9-30/12-31）。</summary>
    public string ReportDate { get; set; } = string.Empty;

    /// <summary>行业关键字（按行业名称模糊匹配，单个）。</summary>
    public string? IndustryContains { get; set; }

    /// <summary>行业多选关键字：行业名称只要包含其中任意一个即视为匹配。</summary>
    public List<string>? IndustryAnyOf { get; set; }

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

    // 估值过滤（来自 RPT_VALUEANALYSIS_DET，单独按需启用）
    /// <summary>市盈率 PE(TTM) 上限。</summary>
    public double? PeMax { get; set; }
    /// <summary>市净率 PB 上限。</summary>
    public double? PbMax { get; set; }
    /// <summary>市销率 PS 上限。</summary>
    public double? PsMax { get; set; }
    /// <summary>股息率(%)下限（如 3 = 3%）。</summary>
    public double? DividendYieldMin { get; set; }

    /// <summary>每页返回数量。</summary>
    public int PageSize { get; set; } = 200;

    /// <summary>是否同时拉取估值数据并过滤（PE/PB/PS/股息率列）。开启后会多发 N 个估值请求，较慢。</summary>
    public bool IncludeValuation { get; set; }
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

    // 估值
    public double? PE { get; set; }
    public double? PB { get; set; }
    public double? PS { get; set; }
    public double? DividendYield { get; set; }   // 单位 %
    public double? TotalMarketCap { get; set; }  // 总市值 元
}
