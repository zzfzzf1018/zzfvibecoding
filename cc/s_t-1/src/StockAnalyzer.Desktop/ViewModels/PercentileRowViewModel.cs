using StockAnalyzer.Core.Models;
using StockAnalyzer.Desktop.Infrastructure;

namespace StockAnalyzer.Desktop.ViewModels;

/// <summary>历史分位表格的一行（对应一个回溯窗口）。</summary>
public sealed class PercentileRowViewModel
{
    public PercentileRowViewModel(PercentileResult result)
    {
        Result = result;
    }

    public PercentileResult Result { get; }

    public string Window => Result.Window.ToDisplayName();

    public string Current => Formatting.Multiple(Result.Current);

    public double? PercentileValue => Result.PercentileRank;

    public string Percentile => Result.PercentileRank is null
        ? "--"
        : $"{Result.PercentileRank.Value:N1}%";

    public string Temperature => Result.Temperature;

    public string Min => Formatting.Multiple(Result.Min);

    public string Q30 => Formatting.Multiple(GetQuantile(0.3));

    public string Median => Formatting.Multiple(Result.Median);

    public string Q70 => Formatting.Multiple(GetQuantile(0.7));

    public string Max => Formatting.Multiple(Result.Max);

    public string Samples => Result.SampleCount == 0
        ? "无样本"
        : $"{Result.SampleCount} 个交易日";

    public string Coverage => Result.SampleCount == 0
        ? "--"
        : Result.IsWindowFullyCovered ? "完整" : "数据不足";

    private double? GetQuantile(double quantile) =>
        Result.Quantiles.TryGetValue(quantile, out double value) ? value : null;
}
