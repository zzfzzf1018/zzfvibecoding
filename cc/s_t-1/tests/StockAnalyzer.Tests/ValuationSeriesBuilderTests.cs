using StockAnalyzer.Core.Analytics;
using StockAnalyzer.Core.Models;
using Xunit;

namespace StockAnalyzer.Tests;

public class ValuationSeriesBuilderTests
{
    private static readonly StockInfo Stock = new()
    {
        Code = "000001",
        Name = "测试股份",
        Market = MarketType.ShenzhenA
    };

    private static List<FinancialReport> BuildReports() => new()
    {
        Report("2023-09-30", "2023-10-30", eps: 2.0, bps: 19),
        Report("2023-12-31", "2024-03-31", eps: 4.0, bps: 20),
        Report("2024-09-30", "2024-10-30", eps: 3.0, bps: 22)
    };

    private static FinancialReport Report(string reportDate, string noticeDate, double eps, double bps) => new()
    {
        Code = Stock.Code,
        Market = Stock.Market,
        ReportDate = DateTime.Parse(reportDate),
        NoticeDate = DateTime.Parse(noticeDate),
        BasicEps = eps,
        BookValuePerShare = bps
    };

    private static DailyBar Bar(string date, double close) => new()
    {
        Code = Stock.Code,
        Market = Stock.Market,
        Date = DateTime.Parse(date),
        Close = close,
        Open = close,
        High = close,
        Low = close
    };

    [Fact]
    public void Build_RestoresTtmFromCumulativeEps()
    {
        var bars = new List<DailyBar> { Bar("2024-11-01", 100) };

        ValuationSeries series = ValuationSeriesBuilder.Build(Stock, bars, BuildReports());

        ValuationPoint point = Assert.Single(series.Points);

        // TTM = 本期累计 3.0 + 上年年报 4.0 - 上年同期 2.0 = 5.0
        Assert.Equal(5.0, point.EpsTtm!.Value, 6);
        Assert.Equal(20.0, point.PeTtm!.Value, 6);
        Assert.Equal(100.0 / 22.0, point.Pb!.Value, 6);
    }

    [Fact]
    public void Build_DoesNotUseReportsBeforeTheirNoticeDate()
    {
        // 该交易日在 2024 三季报公告(10-30)之前，只能看到 2023 年报
        var bars = new List<DailyBar> { Bar("2024-10-29", 100) };

        ValuationSeries series = ValuationSeriesBuilder.Build(Stock, bars, BuildReports());

        ValuationPoint point = Assert.Single(series.Points);
        Assert.Equal(4.0, point.EpsTtm!.Value, 6);
        Assert.Equal(25.0, point.PeTtm!.Value, 6);
    }

    [Fact]
    public void Build_LeavesMetricsNullBeforeFirstReport()
    {
        var bars = new List<DailyBar> { Bar("2023-01-05", 100) };

        ValuationSeries series = ValuationSeriesBuilder.Build(Stock, bars, BuildReports());

        ValuationPoint point = Assert.Single(series.Points);
        Assert.Null(point.EpsTtm);
        Assert.Null(point.PeTtm);
    }

    [Fact]
    public void Build_PrefersDataSourceProvidedTtm()
    {
        var reports = new List<FinancialReport>
        {
            new()
            {
                Code = Stock.Code,
                Market = Stock.Market,
                ReportDate = DateTime.Parse("2024-06-30"),
                NoticeDate = DateTime.Parse("2024-08-30"),
                BasicEps = 1.0,
                EpsTtm = 8.0,
                BookValuePerShare = 10
            }
        };

        var bars = new List<DailyBar> { Bar("2024-09-02", 80) };

        ValuationSeries series = ValuationSeriesBuilder.Build(Stock, bars, reports);

        ValuationPoint point = Assert.Single(series.Points);
        Assert.Equal(8.0, point.EpsTtm!.Value, 6);
        Assert.Equal(10.0, point.PeTtm!.Value, 6);
    }

    [Fact]
    public void Build_CalibratesLatestValuesToQuote()
    {
        var bars = new List<DailyBar> { Bar("2024-11-01", 100) };

        var quote = new StockQuote
        {
            Code = Stock.Code,
            Market = Stock.Market,
            Price = 100,
            PeTtm = 25,   // 与自算的 20 存在口径差异
            Pb = 5
        };

        ValuationSeries series = ValuationSeriesBuilder.Build(Stock, bars, BuildReports(), quote);

        ValuationPoint point = Assert.Single(series.Points);
        Assert.Equal(25.0, point.PeTtm!.Value, 6);
        Assert.Equal(5.0, point.Pb!.Value, 6);
    }

    [Fact]
    public void Build_FallsBackToApproximationWithoutReports()
    {
        var bars = new List<DailyBar> { Bar("2024-10-01", 50), Bar("2024-11-01", 100) };

        var quote = new StockQuote
        {
            Code = Stock.Code,
            Market = Stock.Market,
            Price = 100,
            PeTtm = 20,
            Pb = 4
        };

        ValuationSeries series = ValuationSeriesBuilder.Build(Stock, bars, Array.Empty<FinancialReport>(), quote);

        Assert.Equal(ValuationSeriesQuality.ApproximatedFromLatestQuote, series.Quality);
        Assert.Equal(20.0, series.Points[^1].PeTtm!.Value, 6);
        Assert.Equal(10.0, series.Points[0].PeTtm!.Value, 6);
    }

    [Fact]
    public void Build_ReturnsEmptySeriesWithoutBars()
    {
        ValuationSeries series = ValuationSeriesBuilder.Build(
            Stock, Array.Empty<DailyBar>(), BuildReports());

        Assert.Empty(series.Points);
    }
}
