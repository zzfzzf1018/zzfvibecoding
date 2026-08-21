using StockAnalyzer.Core.Models;

namespace StockAnalyzer.Core.Analytics;

/// <summary>历史估值分位数分析。</summary>
public static class ValuationAnalyzer
{
    /// <summary>估值通道默认分位。</summary>
    public static readonly IReadOnlyList<double> DefaultQuantiles = new[] { 0.1, 0.3, 0.5, 0.7, 0.9 };

    private static readonly double[] StatQuantiles = { 0.1, 0.2, 0.3, 0.5, 0.7, 0.8, 0.9 };

    /// <summary>计算单个窗口的分位数统计。</summary>
    public static PercentileResult CalculatePercentile(
        ValuationSeries series,
        ValuationMetric metric,
        LookbackWindow window,
        DateTime? asOf = null)
    {
        DateTime end = asOf ?? (series.Points.Count > 0 ? series.Points[^1].Date : DateTime.Today);
        DateTime start = end.AddYears(-window.Years());

        var samples = series.Points
            .Where(p => p.Date >= start && p.Date <= end)
            .Select(p => p.GetMetric(metric))
            .Where(v => v is > 0 && !double.IsNaN(v.Value) && !double.IsInfinity(v.Value))
            .Select(v => v!.Value)
            .ToList();

        var result = new PercentileResult
        {
            Metric = metric,
            Window = window,
            StartDate = start,
            EndDate = end,
            SampleCount = samples.Count
        };

        ValuationPoint? latestPoint = series.Points.LastOrDefault(p => p.Date <= end);
        double? current = latestPoint?.GetMetric(metric);

        result.Current = current;

        if (samples.Count == 0)
        {
            return result;
        }

        samples.Sort();

        result.Min = samples[0];
        result.Max = samples[^1];
        result.Median = PercentileCalculator.Median(samples);
        result.Average = samples.Average();
        result.Quantiles = StatQuantiles.ToDictionary(q => q, q => PercentileCalculator.Quantile(samples, q));

        if (current is > 0)
        {
            double rank = PercentileCalculator.PercentRank(samples, current.Value);
            result.PercentileRank = double.IsNaN(rank) ? null : rank;
        }

        ValuationPoint? firstPoint = series.Points.FirstOrDefault(p => p.Date >= start);
        result.IsWindowFullyCovered = firstPoint is not null && (firstPoint.Date - start).TotalDays <= 30;

        return result;
    }

    /// <summary>批量计算 1/3/5/10 年窗口的分位数。</summary>
    public static IReadOnlyList<PercentileResult> CalculateAllWindows(
        ValuationSeries series,
        ValuationMetric metric,
        DateTime? asOf = null)
        => LookbackWindowExtensions.All
            .Select(w => CalculatePercentile(series, metric, w, asOf))
            .ToList();

    /// <summary>
    /// 构建估值通道：把窗口内 PE/PB 的各分位倍数乘以逐日每股锚定值（EPS 或 BPS），
    /// 得到与股价同坐标系的分位价格带。
    /// </summary>
    public static ValuationChannel BuildChannel(
        StockInfo stock,
        ValuationSeries series,
        ValuationMetric metric,
        LookbackWindow window,
        IReadOnlyList<double>? quantiles = null,
        DateTime? asOf = null)
    {
        quantiles ??= DefaultQuantiles;

        DateTime end = asOf ?? (series.Points.Count > 0 ? series.Points[^1].Date : DateTime.Today);
        DateTime start = end.AddYears(-window.Years());

        var windowPoints = series.Points
            .Where(p => p.Date >= start && p.Date <= end)
            .OrderBy(p => p.Date)
            .ToList();

        var channel = new ValuationChannel
        {
            Code = stock.Code,
            Name = stock.Name,
            Market = stock.Market,
            Metric = metric,
            Window = window,
            Quality = series.Quality
        };

        if (windowPoints.Count == 0)
        {
            return channel;
        }

        channel.Dates = windowPoints.Select(p => p.Date).ToList();
        channel.Close = windowPoints.Select(p => p.Close).ToList();

        var samples = windowPoints
            .Select(p => p.GetMetric(metric))
            .Where(v => v is > 0 && !double.IsNaN(v.Value) && !double.IsInfinity(v.Value))
            .Select(v => v!.Value)
            .ToList();

        if (samples.Count == 0)
        {
            return channel;
        }

        samples.Sort();

        var bands = new List<ValuationBand>(quantiles.Count);

        foreach (double q in quantiles.OrderBy(x => x))
        {
            double multiple = PercentileCalculator.Quantile(samples, q);

            var prices = windowPoints
                .Select(p => p.GetAnchor(metric) is > 0 ? multiple * p.GetAnchor(metric)!.Value : (double?)null)
                .ToList();

            bands.Add(new ValuationBand
            {
                Quantile = q,
                MultipleValue = multiple,
                Prices = prices
            });
        }

        channel.Bands = bands;

        double? currentMultiple = windowPoints[^1].GetMetric(metric);
        if (currentMultiple is > 0)
        {
            double rank = PercentileCalculator.PercentRank(samples, currentMultiple.Value);
            channel.CurrentPositionPercent = double.IsNaN(rank) ? null : rank;
        }

        return channel;
    }
}
