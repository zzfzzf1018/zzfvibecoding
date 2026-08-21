using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using StockAnalyzer.Core.Abstractions;
using StockAnalyzer.Core.Models;
using StockAnalyzer.Core.Utils;

namespace StockAnalyzer.Data;

/// <summary>基于 SQLite 的本地持久化实现。</summary>
public sealed class SqliteStockRepository : IStockRepository
{
    private readonly IDbContextFactory<StockDbContext> _contextFactory;
    private readonly ILogger<SqliteStockRepository> _logger;

    public SqliteStockRepository(
        IDbContextFactory<StockDbContext> contextFactory,
        ILogger<SqliteStockRepository> logger)
    {
        _contextFactory = contextFactory;
        _logger = logger;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await using StockDbContext db = await _contextFactory.CreateDbContextAsync(cancellationToken);
        await db.Database.EnsureCreatedAsync(cancellationToken);

        // 提升写入性能与并发读能力
        await db.Database.ExecuteSqlRawAsync("PRAGMA journal_mode=WAL;", cancellationToken);
        await db.Database.ExecuteSqlRawAsync("PRAGMA synchronous=NORMAL;", cancellationToken);

        _logger.LogInformation("本地数据库已就绪。");
    }

    // ------------------------------------------------------------------
    // 股票列表
    // ------------------------------------------------------------------

    public async Task ReplaceStocksAsync(IEnumerable<StockInfo> stocks, CancellationToken cancellationToken = default)
    {
        List<StockInfo> list = Normalize(stocks);

        if (list.Count == 0)
        {
            return;
        }

        await using StockDbContext db = await _contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        var markets = list.Select(s => s.Market).Distinct().ToList();
        await db.Stocks.Where(s => markets.Contains(s.Market)).ExecuteDeleteAsync(cancellationToken);

        db.ChangeTracker.AutoDetectChangesEnabled = false;
        await db.Stocks.AddRangeAsync(list, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        _logger.LogInformation("已全量写入 {Count} 条股票列表记录。", list.Count);
    }

    public async Task UpsertStocksAsync(IEnumerable<StockInfo> stocks, CancellationToken cancellationToken = default)
    {
        List<StockInfo> list = Normalize(stocks);

        if (list.Count == 0)
        {
            return;
        }

        await using StockDbContext db = await _contextFactory.CreateDbContextAsync(cancellationToken);

        foreach (StockInfo stock in list)
        {
            StockInfo? existing = await db.Stocks
                .FirstOrDefaultAsync(s => s.Market == stock.Market && s.Code == stock.Code, cancellationToken);

            if (existing is null)
            {
                db.Stocks.Add(stock);
            }
            else
            {
                existing.Name = stock.Name;
                existing.SecId = stock.SecId;
                existing.NameInitials = stock.NameInitials;
                existing.UpdatedAt = stock.UpdatedAt;
            }
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private static List<StockInfo> Normalize(IEnumerable<StockInfo> stocks)
    {
        var list = stocks
            .Where(s => !string.IsNullOrWhiteSpace(s.Code) && s.Market != MarketType.Unknown)
            .GroupBy(s => (s.Market, s.Code))
            .Select(g => g.First())
            .ToList();

        foreach (StockInfo stock in list)
        {
            stock.NameInitials ??= PinyinHelper.GetInitials(stock.Name);
            stock.UpdatedAt = DateTime.Now;

            if (string.IsNullOrEmpty(stock.SecId))
            {
                stock.SecId = SecurityIdHelper.BuildSecId(stock.Market, stock.Code);
            }
        }

        return list;
    }

    public async Task<int> GetStockCountAsync(MarketGroup group, CancellationToken cancellationToken = default)
    {
        await using StockDbContext db = await _contextFactory.CreateDbContextAsync(cancellationToken);
        return await ApplyGroupFilter(db.Stocks.AsNoTracking(), group).CountAsync(cancellationToken);
    }

    public async Task<StockInfo?> GetStockAsync(MarketType market, string code, CancellationToken cancellationToken = default)
    {
        await using StockDbContext db = await _contextFactory.CreateDbContextAsync(cancellationToken);
        return await db.Stocks.AsNoTracking()
            .FirstOrDefaultAsync(s => s.Market == market && s.Code == code, cancellationToken);
    }

    public async Task<IReadOnlyList<StockInfo>> SearchStocksAsync(
        string keyword,
        MarketGroup group,
        int maxResults,
        CancellationToken cancellationToken = default)
    {
        keyword = keyword?.Trim() ?? string.Empty;
        if (keyword.Length == 0)
        {
            return Array.Empty<StockInfo>();
        }

        string pattern = $"%{EscapeLike(keyword)}%";
        string upper = keyword.ToUpperInvariant();

        await using StockDbContext db = await _contextFactory.CreateDbContextAsync(cancellationToken);

        IQueryable<StockInfo> query = ApplyGroupFilter(db.Stocks.AsNoTracking(), group)
            .Where(s => EF.Functions.Like(s.Code, pattern)
                        || EF.Functions.Like(s.Name, pattern)
                        || (s.NameInitials != null && EF.Functions.Like(s.NameInitials, pattern)));

        // 先取一批候选，再在内存里做相关性排序
        var candidates = await query.Take(Math.Max(maxResults * 8, 200)).ToListAsync(cancellationToken);

        return candidates
            .OrderBy(s => RelevanceScore(s, keyword, upper))
            .ThenBy(s => s.Code, StringComparer.Ordinal)
            .Take(maxResults)
            .ToList();
    }

    private static int RelevanceScore(StockInfo stock, string keyword, string upperKeyword)
    {
        if (string.Equals(stock.Code, keyword, StringComparison.OrdinalIgnoreCase))
        {
            return 0;
        }

        if (stock.Code.StartsWith(keyword, StringComparison.OrdinalIgnoreCase))
        {
            return 1;
        }

        if (string.Equals(stock.Name, keyword, StringComparison.OrdinalIgnoreCase))
        {
            return 2;
        }

        if (stock.Name.StartsWith(keyword, StringComparison.OrdinalIgnoreCase))
        {
            return 3;
        }

        if (stock.NameInitials is not null &&
            stock.NameInitials.StartsWith(upperKeyword, StringComparison.Ordinal))
        {
            return 4;
        }

        if (stock.Name.Contains(keyword, StringComparison.OrdinalIgnoreCase))
        {
            return 5;
        }

        return 6;
    }

    // ------------------------------------------------------------------
    // 自选股
    // ------------------------------------------------------------------

    public async Task<IReadOnlyList<WatchlistItem>> GetWatchlistAsync(CancellationToken cancellationToken = default)
    {
        await using StockDbContext db = await _contextFactory.CreateDbContextAsync(cancellationToken);
        return await db.Watchlist.AsNoTracking()
            .OrderBy(w => w.SortOrder)
            .ThenBy(w => w.AddedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task AddToWatchlistAsync(StockInfo stock, CancellationToken cancellationToken = default)
    {
        await using StockDbContext db = await _contextFactory.CreateDbContextAsync(cancellationToken);

        bool exists = await db.Watchlist
            .AnyAsync(w => w.Market == stock.Market && w.Code == stock.Code, cancellationToken);

        if (exists)
        {
            return;
        }

        int nextOrder = await db.Watchlist.AnyAsync(cancellationToken)
            ? await db.Watchlist.MaxAsync(w => w.SortOrder, cancellationToken) + 1
            : 0;

        db.Watchlist.Add(new WatchlistItem
        {
            Code = stock.Code,
            Market = stock.Market,
            Name = stock.Name,
            SortOrder = nextOrder,
            AddedAt = DateTime.Now
        });

        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task RemoveFromWatchlistAsync(MarketType market, string code, CancellationToken cancellationToken = default)
    {
        await using StockDbContext db = await _contextFactory.CreateDbContextAsync(cancellationToken);
        await db.Watchlist
            .Where(w => w.Market == market && w.Code == code)
            .ExecuteDeleteAsync(cancellationToken);
    }

    public async Task<bool> IsInWatchlistAsync(MarketType market, string code, CancellationToken cancellationToken = default)
    {
        await using StockDbContext db = await _contextFactory.CreateDbContextAsync(cancellationToken);
        return await db.Watchlist.AnyAsync(w => w.Market == market && w.Code == code, cancellationToken);
    }

    public async Task UpdateWatchlistOrderAsync(IReadOnlyList<WatchlistItem> items, CancellationToken cancellationToken = default)
    {
        await using StockDbContext db = await _contextFactory.CreateDbContextAsync(cancellationToken);

        for (int i = 0; i < items.Count; i++)
        {
            WatchlistItem item = items[i];
            int order = i;
            await db.Watchlist
                .Where(w => w.Market == item.Market && w.Code == item.Code)
                .ExecuteUpdateAsync(s => s.SetProperty(w => w.SortOrder, order), cancellationToken);
        }
    }

    public async Task UpdateWatchlistNoteAsync(MarketType market, string code, string? note, CancellationToken cancellationToken = default)
    {
        await using StockDbContext db = await _contextFactory.CreateDbContextAsync(cancellationToken);
        await db.Watchlist
            .Where(w => w.Market == market && w.Code == code)
            .ExecuteUpdateAsync(s => s.SetProperty(w => w.Note, note), cancellationToken);
    }

    // ------------------------------------------------------------------
    // 行情快照
    // ------------------------------------------------------------------

    public async Task SaveQuoteAsync(StockQuote quote, CancellationToken cancellationToken = default)
    {
        await using StockDbContext db = await _contextFactory.CreateDbContextAsync(cancellationToken);

        StockQuote? existing = await db.Quotes
            .FirstOrDefaultAsync(q => q.Market == quote.Market && q.Code == quote.Code, cancellationToken);

        if (existing is null)
        {
            db.Quotes.Add(quote);
        }
        else
        {
            db.Entry(existing).CurrentValues.SetValues(quote);
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<StockQuote?> GetCachedQuoteAsync(MarketType market, string code, CancellationToken cancellationToken = default)
    {
        await using StockDbContext db = await _contextFactory.CreateDbContextAsync(cancellationToken);
        return await db.Quotes.AsNoTracking()
            .FirstOrDefaultAsync(q => q.Market == market && q.Code == code, cancellationToken);
    }

    // ------------------------------------------------------------------
    // 日线
    // ------------------------------------------------------------------

    public async Task SaveDailyBarsAsync(IEnumerable<DailyBar> bars, CancellationToken cancellationToken = default)
    {
        var list = bars
            .GroupBy(b => (b.Market, b.Code, b.Date.Date))
            .Select(g => g.First())
            .ToList();

        if (list.Count == 0)
        {
            return;
        }

        foreach (DailyBar bar in list)
        {
            bar.Date = bar.Date.Date;
        }

        await using StockDbContext db = await _contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        foreach (var group in list.GroupBy(b => (b.Market, b.Code)))
        {
            MarketType market = group.Key.Market;
            string code = group.Key.Code;
            DateTime min = group.Min(b => b.Date);
            DateTime max = group.Max(b => b.Date);

            await db.DailyBars
                .Where(b => b.Market == market && b.Code == code && b.Date >= min && b.Date <= max)
                .ExecuteDeleteAsync(cancellationToken);
        }

        db.ChangeTracker.AutoDetectChangesEnabled = false;
        await db.DailyBars.AddRangeAsync(list, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<DailyBar>> GetDailyBarsAsync(
        MarketType market, string code, DateTime start, DateTime end,
        CancellationToken cancellationToken = default)
    {
        start = start.Date;
        end = end.Date;

        await using StockDbContext db = await _contextFactory.CreateDbContextAsync(cancellationToken);
        return await db.DailyBars.AsNoTracking()
            .Where(b => b.Market == market && b.Code == code && b.Date >= start && b.Date <= end)
            .OrderBy(b => b.Date)
            .ToListAsync(cancellationToken);
    }

    public async Task<DateTime?> GetLastBarDateAsync(MarketType market, string code, CancellationToken cancellationToken = default)
    {
        await using StockDbContext db = await _contextFactory.CreateDbContextAsync(cancellationToken);
        return await db.DailyBars.AsNoTracking()
            .Where(b => b.Market == market && b.Code == code)
            .OrderByDescending(b => b.Date)
            .Select(b => (DateTime?)b.Date)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<DateTime?> GetFirstBarDateAsync(MarketType market, string code, CancellationToken cancellationToken = default)
    {
        await using StockDbContext db = await _contextFactory.CreateDbContextAsync(cancellationToken);
        return await db.DailyBars.AsNoTracking()
            .Where(b => b.Market == market && b.Code == code)
            .OrderBy(b => b.Date)
            .Select(b => (DateTime?)b.Date)
            .FirstOrDefaultAsync(cancellationToken);
    }

    // ------------------------------------------------------------------
    // 财报
    // ------------------------------------------------------------------

    public async Task SaveFinancialReportsAsync(IEnumerable<FinancialReport> reports, CancellationToken cancellationToken = default)
    {
        var list = reports
            .GroupBy(r => (r.Market, r.Code, r.ReportDate.Date))
            .Select(g => g.First())
            .ToList();

        if (list.Count == 0)
        {
            return;
        }

        foreach (FinancialReport report in list)
        {
            report.ReportDate = report.ReportDate.Date;
            report.NoticeDate = report.NoticeDate.Date;
        }

        await using StockDbContext db = await _contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        foreach (var group in list.GroupBy(r => (r.Market, r.Code)))
        {
            MarketType market = group.Key.Market;
            string code = group.Key.Code;
            await db.FinancialReports
                .Where(r => r.Market == market && r.Code == code)
                .ExecuteDeleteAsync(cancellationToken);
        }

        await db.FinancialReports.AddRangeAsync(list, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<FinancialReport>> GetFinancialReportsAsync(
        MarketType market, string code, CancellationToken cancellationToken = default)
    {
        await using StockDbContext db = await _contextFactory.CreateDbContextAsync(cancellationToken);
        return await db.FinancialReports.AsNoTracking()
            .Where(r => r.Market == market && r.Code == code)
            .OrderBy(r => r.ReportDate)
            .ToListAsync(cancellationToken);
    }

    // ------------------------------------------------------------------
    // 同步元数据 / 统计
    // ------------------------------------------------------------------

    public async Task SetSyncStampAsync(string key, DateTime timestamp, CancellationToken cancellationToken = default)
    {
        await using StockDbContext db = await _contextFactory.CreateDbContextAsync(cancellationToken);

        SyncStamp? existing = await db.SyncStamps.FirstOrDefaultAsync(s => s.Key == key, cancellationToken);
        if (existing is null)
        {
            db.SyncStamps.Add(new SyncStamp { Key = key, UpdatedAt = timestamp });
        }
        else
        {
            existing.UpdatedAt = timestamp;
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<DateTime?> GetSyncStampAsync(string key, CancellationToken cancellationToken = default)
    {
        await using StockDbContext db = await _contextFactory.CreateDbContextAsync(cancellationToken);
        return await db.SyncStamps.AsNoTracking()
            .Where(s => s.Key == key)
            .Select(s => (DateTime?)s.UpdatedAt)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyDictionary<string, long>> GetStorageStatisticsAsync(CancellationToken cancellationToken = default)
    {
        await using StockDbContext db = await _contextFactory.CreateDbContextAsync(cancellationToken);

        return new Dictionary<string, long>
        {
            ["股票列表"] = await db.Stocks.CountAsync(cancellationToken),
            ["自选股"] = await db.Watchlist.CountAsync(cancellationToken),
            ["行情快照"] = await db.Quotes.CountAsync(cancellationToken),
            ["日线记录"] = await db.DailyBars.CountAsync(cancellationToken),
            ["财报记录"] = await db.FinancialReports.CountAsync(cancellationToken)
        };
    }

    public async Task ClearHistoryCacheAsync(CancellationToken cancellationToken = default)
    {
        await using StockDbContext db = await _contextFactory.CreateDbContextAsync(cancellationToken);
        await db.DailyBars.ExecuteDeleteAsync(cancellationToken);
        await db.FinancialReports.ExecuteDeleteAsync(cancellationToken);
        await db.SyncStamps.ExecuteDeleteAsync(cancellationToken);
        await db.Database.ExecuteSqlRawAsync("VACUUM;", cancellationToken);
    }

    private static IQueryable<StockInfo> ApplyGroupFilter(IQueryable<StockInfo> query, MarketGroup group) => group switch
    {
        MarketGroup.AShare => query.Where(s => s.Market != MarketType.HongKong && s.Market != MarketType.Unknown),
        MarketGroup.HongKong => query.Where(s => s.Market == MarketType.HongKong),
        _ => query
    };

    // SQLite 的 LIKE 通配符无法在 EF.Functions.Like 中方便地转义，直接移除以避免注入式匹配
    private static string EscapeLike(string value) =>
        value.Replace("%", string.Empty).Replace("_", string.Empty);
}
