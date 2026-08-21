namespace StockAnalyzer.Core.Models;

/// <summary>估值指标类型。</summary>
public enum ValuationMetric
{
    /// <summary>市盈率 TTM。</summary>
    PeTtm = 0,

    /// <summary>市净率。</summary>
    Pb = 1
}

/// <summary>逐日估值序列的一个点。</summary>
public sealed class ValuationPoint
{
    public DateTime Date { get; set; }

    /// <summary>当日收盘价（不复权）。</summary>
    public double Close { get; set; }

    /// <summary>当日可见的滚动每股收益（TTM）。</summary>
    public double? EpsTtm { get; set; }

    /// <summary>当日可见的每股净资产。</summary>
    public double? Bps { get; set; }

    /// <summary>市盈率 TTM。负值表示亏损，通常在统计中剔除。</summary>
    public double? PeTtm { get; set; }

    /// <summary>市净率。</summary>
    public double? Pb { get; set; }

    public double? GetMetric(ValuationMetric metric) =>
        metric == ValuationMetric.PeTtm ? PeTtm : Pb;

    /// <summary>指标对应的每股锚定值（EPS 或 BPS），用于把估值倍数换算回价格。</summary>
    public double? GetAnchor(ValuationMetric metric) =>
        metric == ValuationMetric.PeTtm ? EpsTtm : Bps;
}

/// <summary>估值序列的数据来源与可信度。</summary>
public enum ValuationSeriesQuality
{
    /// <summary>由真实定期报告（EPS/BPS）逐日对齐推导，精度最高。</summary>
    FromFinancialReports = 0,

    /// <summary>缺少财报，使用当前 PE/PB 反推每股指标后按价格外推，仅供参考。</summary>
    ApproximatedFromLatestQuote = 1
}

/// <summary>完整的逐日估值序列。</summary>
public sealed class ValuationSeries
{
    public string Code { get; set; } = string.Empty;

    public MarketType Market { get; set; }

    public IReadOnlyList<ValuationPoint> Points { get; set; } = Array.Empty<ValuationPoint>();

    public ValuationSeriesQuality Quality { get; set; }

    public DateTime GeneratedAt { get; set; }

    public string QualityDescription => Quality switch
    {
        ValuationSeriesQuality.FromFinancialReports => "基于定期报告 EPS/BPS 时点对齐计算",
        _ => "缺少财报数据，按最新 PE/PB 反推每股指标近似计算（仅供参考）"
    };
}
