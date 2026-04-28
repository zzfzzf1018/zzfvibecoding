using ComparetoolWpf.Models;

namespace ComparetoolWpf.Services;

/// <summary>
/// 数据访问门面：先查 SQLite 缓存，缺数据再调用东方财富，并把新数据回写缓存。
/// </summary>
public class StockDataService
{
    private readonly EastMoneyService _api;
    private readonly ReportCache _cache;

    public StockDataService(EastMoneyService api, ReportCache cache)
    {
        _api = api;
        _cache = cache;
    }

    public Task<List<StockInfo>> SearchStocksAsync(string keyword, CancellationToken ct = default)
        => _api.SearchStocksAsync(keyword, ct);

    /// <summary>
    /// 获取报表（带缓存）。
    /// 流程：
    ///   1) 读取本地缓存中匹配 stock+kind 的全部记录；
    ///   2) 若缓存中已有 >= <paramref name="minPeriods"/> 期且最新更新不超过 <paramref name="refreshAfterDays"/> 天，
    ///      直接返回（按 periodType 过滤）；
    ///   3) 否则调用东方财富，写回缓存后再返回过滤结果。
    /// </summary>
    public async Task<List<FinancialReport>> GetReportsAsync(
        StockInfo stock,
        ReportKind kind,
        ReportPeriodType periodType = ReportPeriodType.All,
        int pageSize = 20,
        bool forceRefresh = false,
        int minPeriods = 4,
        int refreshAfterDays = 7,
        CancellationToken ct = default)
    {
        if (!forceRefresh)
        {
            var cached = _cache.Load(stock.FullCode, kind);
            bool fresh = cached.Count > 0
                         && (DateTime.UtcNow - cached.Max(r => r.ReportDate.ToUniversalTime())).TotalDays < 365;
            if (cached.Count >= minPeriods && fresh)
            {
                return FilterAndOrder(cached, periodType, pageSize);
            }
        }

        var fetched = await _api.GetReportsAsync(stock, kind, ReportPeriodType.All, pageSize, ct);
        if (fetched.Count > 0) _cache.Save(fetched);

        return FilterAndOrder(fetched, periodType, pageSize);
    }

    private static List<FinancialReport> FilterAndOrder(
        IEnumerable<FinancialReport> src, ReportPeriodType pt, int pageSize)
    {
        IEnumerable<FinancialReport> q = src;
        if (pt != ReportPeriodType.All)
        {
            q = q.Where(r => MatchPeriod(r.ReportDate, pt));
        }
        return q.OrderByDescending(r => r.ReportDate).Take(pageSize).ToList();
    }

    private static bool MatchPeriod(DateTime d, ReportPeriodType pt) => pt switch
    {
        ReportPeriodType.Annual => d.Month == 12 && d.Day == 31,
        ReportPeriodType.SemiAnnual => d.Month == 6 && d.Day == 30,
        ReportPeriodType.Q1 => d.Month == 3 && d.Day == 31,
        ReportPeriodType.Q3 => d.Month == 9 && d.Day == 30,
        _ => true,
    };

    /// <summary>清空缓存。</summary>
    public void ClearCache() => _cache.Clear();
}
