using StockTechAnalyzer.Models;
using StockTechAnalyzer.Storage;

namespace StockTechAnalyzer.Services;

/// <summary>
/// 缓存装饰器：成功取数后写入 SQLite；网络失败时回退到本地缓存。
/// </summary>
public sealed class CachedDataSource : IStockDataSource
{
    private readonly IStockDataSource _inner;
    private readonly KlineCache _cache;

    public CachedDataSource(IStockDataSource inner, KlineCache cache)
    {
        _inner = inner;
        _cache = cache;
    }

    public string Name => _inner.Name + " + 本地缓存";

    public Task<IReadOnlyList<StockInfo>> SearchAsync(string keyword, CancellationToken ct = default)
        => _inner.SearchAsync(keyword, ct);

    public Task<RealtimeQuote?> GetQuoteAsync(StockInfo stock, CancellationToken ct = default)
        => _inner.GetQuoteAsync(stock, ct);

    public async Task<IReadOnlyList<Kline>> GetKlineAsync(StockInfo stock, KlinePeriod period, int count, CancellationToken ct = default)
    {
        try
        {
            var bars = await _inner.GetKlineAsync(stock, period, count, ct).ConfigureAwait(false);
            if (bars.Count > 0)
            {
                try { _cache.Save(stock.FullCode, period, bars); } catch { /* 写缓存失败忽略 */ }
            }
            return bars;
        }
        catch when (!ct.IsCancellationRequested)
        {
            // 网络失败 → 回退缓存
            var cached = _cache.Load(stock.FullCode, period, count);
            if (cached.Count > 0) return cached;
            throw;
        }
    }
}
