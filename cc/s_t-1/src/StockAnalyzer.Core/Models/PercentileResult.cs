namespace StockAnalyzer.Core.Models;

/// <summary>回溯窗口。</summary>
public enum LookbackWindow
{
    OneYear = 1,
    ThreeYears = 3,
    FiveYears = 5,
    TenYears = 10
}

public static class LookbackWindowExtensions
{
    public static int Years(this LookbackWindow window) => (int)window;

    public static string ToDisplayName(this LookbackWindow window) => window switch
    {
        LookbackWindow.OneYear => "近 1 年",
        LookbackWindow.ThreeYears => "近 3 年",
        LookbackWindow.FiveYears => "近 5 年",
        LookbackWindow.TenYears => "近 10 年",
        _ => window.ToString()
    };

    public static IReadOnlyList<LookbackWindow> All { get; } = new[]
    {
        LookbackWindow.OneYear,
        LookbackWindow.ThreeYears,
        LookbackWindow.FiveYears,
        LookbackWindow.TenYears
    };
}

/// <summary>某一窗口下的历史分位数统计结果。</summary>
public sealed class PercentileResult
{
    public ValuationMetric Metric { get; set; }

    public LookbackWindow Window { get; set; }

    /// <summary>窗口起始日期。</summary>
    public DateTime StartDate { get; set; }

    public DateTime EndDate { get; set; }

    /// <summary>参与统计的有效样本数（已剔除空值与非正值）。</summary>
    public int SampleCount { get; set; }

    /// <summary>当前值。</summary>
    public double? Current { get; set; }

    /// <summary>当前值在窗口内的百分位（0~100）。</summary>
    public double? PercentileRank { get; set; }

    public double? Min { get; set; }

    public double? Max { get; set; }

    public double? Median { get; set; }

    public double? Average { get; set; }

    /// <summary>分位点数值：key 为分位（0~1），value 为对应的指标值。</summary>
    public IReadOnlyDictionary<double, double> Quantiles { get; set; } =
        new Dictionary<double, double>();

    /// <summary>数据是否足以覆盖整个窗口（样本起始日晚于窗口起始日超过 30 天则为 false）。</summary>
    public bool IsWindowFullyCovered { get; set; }

    /// <summary>估值温度描述。</summary>
    public string Temperature => PercentileRank switch
    {
        null => "无数据",
        < 10 => "极度低估",
        < 30 => "低估",
        < 70 => "合理",
        < 90 => "高估",
        _ => "极度高估"
    };
}
