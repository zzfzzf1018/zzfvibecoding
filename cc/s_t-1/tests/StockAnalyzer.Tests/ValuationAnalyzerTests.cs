using StockAnalyzer.Core.Analytics;
using StockAnalyzer.Core.Models;
using Xunit;

namespace StockAnalyzer.Tests;

public class ValuationAnalyzerTests
{
    private static readonly StockInfo Stock = new()
    {
        Code = "600000",
        Name = "测试银行",
        Market = MarketType.ShanghaiA
    };

    /// <summary>构造 EPS 恒为 1 的序列，使 PE 数值等于收盘价，便于断言。</summary>
    private static ValuationSeries BuildLinearSeries(int days, DateTime end)
    {
        var points = new List<ValuationPoint>(days);

        for (int i = 0; i < days; i++)
        {
            double price = i + 1;
            points.Add(new ValuationPoint
            {
                Date = end.AddDays(-(days - 1 - i)),
                Close = price,
                EpsTtm = 1,
                Bps = 1,
                PeTtm = price,
                Pb = price
            });
        }

        return new ValuationSeries
        {
            Code = Stock.Code,
            Market = Stock.Market,
            Points = points,
            Quality = ValuationSeriesQuality.FromFinancialReports
        };
    }

    [Fact]
    public void CalculatePercentile_ReportsCurrentAtTopWhenPriceRises()
    {
        DateTime end = new(2025, 1, 1);
        ValuationSeries series = BuildLinearSeries(200, end);

        PercentileResult result = ValuationAnalyzer.CalculatePercentile(
            series, ValuationMetric.PeTtm, LookbackWindow.OneYear, end);

        Assert.Equal(200, result.SampleCount);
        Assert.Equal(200, result.Current!.Value, 6);
        Assert.Equal(100, result.PercentileRank!.Value, 6);
        Assert.Equal(1, result.Min!.Value, 6);
        Assert.Equal(200, result.Max!.Value, 6);
        Assert.Equal("极度高估", result.Temperature);
    }

    [Fact]
    public void CalculatePercentile_MarksWindowAsNotFullyCovered()
    {
        DateTime end = new(2025, 1, 1);

        // 仅 200 天数据，无法覆盖 10 年窗口
        PercentileResult result = ValuationAnalyzer.CalculatePercentile(
            BuildLinearSeries(200, end), ValuationMetric.PeTtm, LookbackWindow.TenYears, end);

        Assert.False(result.IsWindowFullyCovered);
    }

    [Fact]
    public void CalculatePercentile_IgnoresNonPositiveSamples()
    {
        DateTime end = new(2025, 1, 1);

        var series = new ValuationSeries
        {
            Points = new List<ValuationPoint>
            {
                new() { Date = end.AddDays(-2), Close = 10, EpsTtm = -1, PeTtm = null },
                new() { Date = end.AddDays(-1), Close = 10, EpsTtm = 1, PeTtm = 10 },
                new() { Date = end, Close = 20, EpsTtm = 1, PeTtm = 20 }
            }
        };

        PercentileResult result = ValuationAnalyzer.CalculatePercentile(
            series, ValuationMetric.PeTtm, LookbackWindow.OneYear, end);

        Assert.Equal(2, result.SampleCount);
    }

    [Fact]
    public void CalculateAllWindows_ReturnsFourWindows()
    {
        DateTime end = new(2025, 1, 1);

        IReadOnlyList<PercentileResult> results = ValuationAnalyzer.CalculateAllWindows(
            BuildLinearSeries(300, end), ValuationMetric.Pb, end);

        Assert.Equal(4, results.Count);
        Assert.Equal(
            new[] { LookbackWindow.OneYear, LookbackWindow.ThreeYears, LookbackWindow.FiveYears, LookbackWindow.TenYears },
            results.Select(r => r.Window));
    }

    [Fact]
    public void BuildChannel_ProducesAscendingBands()
    {
        DateTime end = new(2025, 1, 1);
        ValuationSeries series = BuildLinearSeries(300, end);

        ValuationChannel channel = ValuationAnalyzer.BuildChannel(
            Stock, series, ValuationMetric.PeTtm, LookbackWindow.OneYear, asOf: end);

        Assert.Equal(5, channel.Bands.Count);
        Assert.Equal(365 >= 300 ? 300 : 365, channel.Dates.Count);

        double[] multiples = channel.Bands.Select(b => b.MultipleValue).ToArray();
        Assert.Equal(multiples.OrderBy(m => m), multiples);

        // EPS 恒为 1，因此分位价格 = 分位倍数
        foreach (ValuationBand band in channel.Bands)
        {
            Assert.Equal(band.MultipleValue, band.Prices[^1]!.Value, 6);
        }
    }

    [Fact]
    public void BuildChannel_ReturnsEmptyWhenNoData()
    {
        var series = new ValuationSeries { Points = Array.Empty<ValuationPoint>() };

        ValuationChannel channel = ValuationAnalyzer.BuildChannel(
            Stock, series, ValuationMetric.PeTtm, LookbackWindow.FiveYears);

        Assert.Empty(channel.Dates);
        Assert.Empty(channel.Bands);
        Assert.Null(channel.CurrentPositionPercent);
    }
}
