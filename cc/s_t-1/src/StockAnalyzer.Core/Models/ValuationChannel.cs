namespace StockAnalyzer.Core.Models;

/// <summary>估值通道中的一条分位带。</summary>
public sealed class ValuationBand
{
    /// <summary>分位，取值 0~1，例如 0.1 / 0.3 / 0.5 / 0.7 / 0.9。</summary>
    public double Quantile { get; set; }

    /// <summary>该分位对应的估值倍数（PE 或 PB）。</summary>
    public double MultipleValue { get; set; }

    /// <summary>该分位带逐日折算出的价格（倍数 × 当日每股锚定值）。</summary>
    public IReadOnlyList<double?> Prices { get; set; } = Array.Empty<double?>();

    public string Label => $"{Quantile * 100:0.#}% 分位（{MultipleValue:0.##}x）";
}

/// <summary>估值通道计算结果（对标东财 App 的“估值通道”）。</summary>
public sealed class ValuationChannel
{
    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public MarketType Market { get; set; }

    public ValuationMetric Metric { get; set; }

    public LookbackWindow Window { get; set; }

    /// <summary>横轴日期。</summary>
    public IReadOnlyList<DateTime> Dates { get; set; } = Array.Empty<DateTime>();

    /// <summary>实际收盘价（不复权）。</summary>
    public IReadOnlyList<double> Close { get; set; } = Array.Empty<double>();

    /// <summary>由低到高排列的分位带。</summary>
    public IReadOnlyList<ValuationBand> Bands { get; set; } = Array.Empty<ValuationBand>();

    /// <summary>当前价所处的通道位置（0~100），即当前估值在窗口内的百分位。</summary>
    public double? CurrentPositionPercent { get; set; }

    public ValuationSeriesQuality Quality { get; set; }

    public string MetricName => Metric == ValuationMetric.PeTtm ? "市盈率(TTM)" : "市净率(PB)";
}
