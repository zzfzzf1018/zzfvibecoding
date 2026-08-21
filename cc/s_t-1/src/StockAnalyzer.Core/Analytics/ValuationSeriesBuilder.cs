using StockAnalyzer.Core.Models;

namespace StockAnalyzer.Core.Analytics;

/// <summary>
/// 把「日线价格」与「定期报告每股指标」合成为逐日 PE(TTM) / PB 序列。
/// </summary>
/// <remarks>
/// 口径说明：
/// 1. 价格使用 <b>不复权</b> 收盘价，与当期报告的每股指标（同样基于当时股本）保持一致；
/// 2. EPS(TTM) 由累计口径的基本每股收益还原：
///    <c>TTM = 本期累计 + 上年年报累计 - 上年同期累计</c>，四季报直接取年报累计；
/// 3. 采用「公告日可见」对齐，即某交易日只会用到该日之前已披露的报告，避免未来函数；
/// 4. 缺少财报时退化为 <see cref="ValuationSeriesQuality.ApproximatedFromLatestQuote"/> 近似模式。
/// </remarks>
public static class ValuationSeriesBuilder
{
    /// <summary>基于财报构建逐日估值序列。</summary>
    public static ValuationSeries Build(
        StockInfo stock,
        IReadOnlyList<DailyBar> bars,
        IReadOnlyList<FinancialReport> reports,
        StockQuote? latestQuote = null)
    {
        if (bars.Count == 0)
        {
            return new ValuationSeries
            {
                Code = stock.Code,
                Market = stock.Market,
                Points = Array.Empty<ValuationPoint>(),
                Quality = ValuationSeriesQuality.ApproximatedFromLatestQuote,
                GeneratedAt = DateTime.Now
            };
        }

        IReadOnlyList<PointInTimeFundamental> timeline = BuildFundamentalTimeline(reports);

        ValuationSeries series = timeline.Count > 0
            ? BuildFromTimeline(stock, bars, timeline)
            : BuildApproximation(stock, bars, latestQuote);

        CalibrateToQuote(series, latestQuote);
        return series;
    }

    /// <summary>
    /// 把序列末端的 PE/PB 对齐到行情快照。
    /// </summary>
    /// <remarks>
    /// 财报口径与交易所行情口径会有系统性偏差（最典型的是港股：报表用人民币、报价用港元，
    /// A 股则是“基本每股收益”与“归母净利润/总股本”的差异）。
    /// 这里用一个常数因子缩放整条 EPS/BPS 序列，使当前值与市场报价一致。
    /// 由于是单调缩放，<b>历史分位数与估值通道价格带均不受影响</b>，只是把绝对倍数校准到通行口径。
    /// </remarks>
    private static void CalibrateToQuote(ValuationSeries series, StockQuote? quote)
    {
        if (quote is null || series.Points.Count == 0)
        {
            return;
        }

        ValuationPoint last = series.Points[^1];

        double? peFactor = ResolveFactor(last.PeTtm, quote.PeTtm);
        double? pbFactor = ResolveFactor(last.Pb, quote.Pb);

        if (peFactor is null && pbFactor is null)
        {
            return;
        }

        foreach (ValuationPoint point in series.Points)
        {
            if (peFactor is { } pe)
            {
                point.EpsTtm = point.EpsTtm * pe;
                point.PeTtm = point.PeTtm / pe;
            }

            if (pbFactor is { } pb)
            {
                point.Bps = point.Bps * pb;
                point.Pb = point.Pb / pb;
            }
        }
    }

    /// <summary>计算校准因子；偏离过大时视为数据异常，不做校准。</summary>
    private static double? ResolveFactor(double? computed, double? quoted)
    {
        if (computed is not > 0 || quoted is not > 0)
        {
            return null;
        }

        double factor = computed.Value / quoted.Value;
        return factor is > 0.2 and < 5.0 ? factor : null;
    }

    private static ValuationSeries BuildFromTimeline(
        StockInfo stock,
        IReadOnlyList<DailyBar> bars,
        IReadOnlyList<PointInTimeFundamental> timeline)
    {
        var ordered = bars.OrderBy(b => b.Date).ToList();
        var points = new List<ValuationPoint>(ordered.Count);
        int cursor = -1;

        foreach (DailyBar bar in ordered)
        {
            // 前移游标到最后一条「公告日 <= 当前交易日」的记录
            while (cursor + 1 < timeline.Count && timeline[cursor + 1].VisibleFrom.Date <= bar.Date.Date)
            {
                cursor++;
            }

            PointInTimeFundamental? current = cursor >= 0 ? timeline[cursor] : null;
            double? eps = current?.EpsTtm;
            double? bps = current?.Bps;

            points.Add(new ValuationPoint
            {
                Date = bar.Date,
                Close = bar.Close,
                EpsTtm = eps,
                Bps = bps,
                PeTtm = eps is > 0 ? bar.Close / eps.Value : null,
                Pb = bps is > 0 ? bar.Close / bps.Value : null
            });
        }

        return new ValuationSeries
        {
            Code = stock.Code,
            Market = stock.Market,
            Points = points,
            Quality = ValuationSeriesQuality.FromFinancialReports,
            GeneratedAt = DateTime.Now
        };
    }

    /// <summary>
    /// 无财报时的退化方案：用最新快照的 PE/PB 反推每股 EPS、BPS，并假设其在窗口内恒定。
    /// 此时 PE/PB 曲线形状与价格完全一致，只能用于观察相对位置。
    /// </summary>
    private static ValuationSeries BuildApproximation(
        StockInfo stock,
        IReadOnlyList<DailyBar> bars,
        StockQuote? latestQuote)
    {
        var ordered = bars.OrderBy(b => b.Date).ToList();
        double lastClose = ordered[^1].Close;
        double referencePrice = latestQuote?.Price is > 0 ? latestQuote.Price!.Value : lastClose;

        double? eps = latestQuote?.PeTtm is > 0 ? referencePrice / latestQuote.PeTtm!.Value : null;
        double? bps = latestQuote?.Pb is > 0
            ? referencePrice / latestQuote.Pb!.Value
            : latestQuote?.BookValuePerShare is > 0
                ? latestQuote.BookValuePerShare
                : null;

        var points = ordered.Select(bar => new ValuationPoint
        {
            Date = bar.Date,
            Close = bar.Close,
            EpsTtm = eps,
            Bps = bps,
            PeTtm = eps is > 0 ? bar.Close / eps.Value : null,
            Pb = bps is > 0 ? bar.Close / bps.Value : null
        }).ToList();

        return new ValuationSeries
        {
            Code = stock.Code,
            Market = stock.Market,
            Points = points,
            Quality = ValuationSeriesQuality.ApproximatedFromLatestQuote,
            GeneratedAt = DateTime.Now
        };
    }

    /// <summary>把定期报告转换为按公告日排序的「时点可见」基本面时间线。</summary>
    internal static IReadOnlyList<PointInTimeFundamental> BuildFundamentalTimeline(
        IReadOnlyList<FinancialReport> reports)
    {
        if (reports.Count == 0)
        {
            return Array.Empty<PointInTimeFundamental>();
        }

        // 同一报告期可能被修正多次，按报告期去重保留公告日最早的一条
        var byPeriod = reports
            .Where(r => r.ReportDate != default)
            .GroupBy(r => r.ReportDate.Date)
            .Select(g => g.OrderBy(r => r.NoticeDate == default ? DateTime.MaxValue : r.NoticeDate).First())
            .OrderBy(r => r.ReportDate)
            .ToList();

        var cumulativeByPeriod = byPeriod
            .Where(r => r.BasicEps.HasValue)
            .ToDictionary(r => (r.Year, r.Quarter), r => r.BasicEps!.Value);

        var result = new List<PointInTimeFundamental>(byPeriod.Count);

        foreach (FinancialReport report in byPeriod)
        {
            double? epsTtm = ComputeEpsTtm(report, cumulativeByPeriod);

            result.Add(new PointInTimeFundamental
            {
                ReportDate = report.ReportDate,
                // 公告日缺失时，保守假设报告期结束后 45 天才可见
                VisibleFrom = report.NoticeDate == default
                    ? report.ReportDate.AddDays(45)
                    : report.NoticeDate,
                EpsTtm = epsTtm,
                Bps = report.BookValuePerShare
            });
        }

        // 按可见日排序，并让 EPS/BPS 在缺失时沿用上一期，避免曲线断裂
        var ordered = result.OrderBy(r => r.VisibleFrom).ThenBy(r => r.ReportDate).ToList();
        double? lastEps = null;
        double? lastBps = null;

        foreach (PointInTimeFundamental item in ordered)
        {
            item.EpsTtm ??= lastEps;
            item.Bps ??= lastBps;
            lastEps = item.EpsTtm;
            lastBps = item.Bps;
        }

        return ordered;
    }

    /// <summary>由累计每股收益还原 TTM；若数据源已直接给出 TTM 则优先采用。</summary>
    internal static double? ComputeEpsTtm(
        FinancialReport report,
        IReadOnlyDictionary<(int Year, int Quarter), double> cumulative)
    {
        if (report.EpsTtm is { } provided && provided != 0)
        {
            return provided;
        }

        if (report.BasicEps is not { } currentCumulative)
        {
            return null;
        }

        if (report.Quarter == 4)
        {
            return currentCumulative;
        }

        bool hasLastAnnual = cumulative.TryGetValue((report.Year - 1, 4), out double lastAnnual);
        bool hasLastSame = cumulative.TryGetValue((report.Year - 1, report.Quarter), out double lastSamePeriod);

        if (hasLastAnnual && hasLastSame)
        {
            return currentCumulative + lastAnnual - lastSamePeriod;
        }

        // 缺少上年数据时按当期进度简单年化
        return currentCumulative * 4.0 / report.Quarter;
    }

    internal sealed class PointInTimeFundamental
    {
        public DateTime ReportDate { get; set; }

        public DateTime VisibleFrom { get; set; }

        public double? EpsTtm { get; set; }

        public double? Bps { get; set; }
    }
}
