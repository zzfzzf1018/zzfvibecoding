using StockAnalyzer.Core.Models;

namespace StockAnalyzer.Core.Abstractions;

/// <summary>本地持久化仓储抽象（SQLite 实现）。</summary>
public interface IStockRepository
{
    Task InitializeAsync(CancellationToken cancellationToken = default);

    // ---------- 股票列表 ----------

    /// <summary>整体替换指定市场的股票列表（用于全量同步）。</summary>
    Task ReplaceStocksAsync(IEnumerable<StockInfo> stocks, CancellationToken cancellationToken = default);

    /// <summary>增量合并少量股票信息（用于在线搜索结果落库），不会删除已有数据。</summary>
    Task UpsertStocksAsync(IEnumerable<StockInfo> stocks, CancellationToken cancellationToken = default);

    Task<int> GetStockCountAsync(MarketGroup group, CancellationToken cancellationToken = default);

    Task<StockInfo?> GetStockAsync(MarketType market, string code, CancellationToken cancellationToken = default);

    /// <summary>按代码 / 名称模糊检索。</summary>
    Task<IReadOnlyList<StockInfo>> SearchStocksAsync(
        string keyword,
        MarketGroup group,
        int maxResults,
        CancellationToken cancellationToken = default);

    // ---------- 自选股 ----------
    Task<IReadOnlyList<WatchlistItem>> GetWatchlistAsync(CancellationToken cancellationToken = default);

    Task AddToWatchlistAsync(StockInfo stock, CancellationToken cancellationToken = default);

    Task RemoveFromWatchlistAsync(MarketType market, string code, CancellationToken cancellationToken = default);

    Task<bool> IsInWatchlistAsync(MarketType market, string code, CancellationToken cancellationToken = default);

    Task UpdateWatchlistOrderAsync(IReadOnlyList<WatchlistItem> items, CancellationToken cancellationToken = default);

    Task UpdateWatchlistNoteAsync(MarketType market, string code, string? note, CancellationToken cancellationToken = default);

    // ---------- 行情快照 ----------
    Task SaveQuoteAsync(StockQuote quote, CancellationToken cancellationToken = default);

    Task<StockQuote?> GetCachedQuoteAsync(MarketType market, string code, CancellationToken cancellationToken = default);

    // ---------- 日线 ----------
    Task SaveDailyBarsAsync(IEnumerable<DailyBar> bars, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DailyBar>> GetDailyBarsAsync(
        MarketType market, string code, DateTime start, DateTime end,
        CancellationToken cancellationToken = default);

    Task<DateTime?> GetLastBarDateAsync(MarketType market, string code, CancellationToken cancellationToken = default);

    Task<DateTime?> GetFirstBarDateAsync(MarketType market, string code, CancellationToken cancellationToken = default);

    // ---------- 财报 ----------
    Task SaveFinancialReportsAsync(IEnumerable<FinancialReport> reports, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<FinancialReport>> GetFinancialReportsAsync(
        MarketType market, string code, CancellationToken cancellationToken = default);

    // ---------- 同步元数据 ----------
    Task SetSyncStampAsync(string key, DateTime timestamp, CancellationToken cancellationToken = default);

    Task<DateTime?> GetSyncStampAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>返回数据库文件大小（字节）与主要表行数，用于“本地数据”页展示。</summary>
    Task<IReadOnlyDictionary<string, long>> GetStorageStatisticsAsync(CancellationToken cancellationToken = default);

    Task ClearHistoryCacheAsync(CancellationToken cancellationToken = default);
}
