using System.Collections.ObjectModel;
using ComparetoolWpf.Models;

namespace ComparetoolWpf.Services;

/// <summary>
/// 自选股共享服务：包装 <see cref="ReportCache"/> 中的 Watchlist 表，
/// 暴露一个进程级 <see cref="ObservableCollection{StockInfo}"/> 让所有
/// Tab 实时同步（任何一处增删都立刻反映到所有引用此集合的 UI）。
/// </summary>
public class WatchlistService
{
    private readonly ReportCache _cache;

    public WatchlistService(ReportCache cache)
    {
        _cache = cache;
        Items = new ObservableCollection<StockInfo>(_cache.LoadWatchlist());
    }

    /// <summary>共享集合，UI 直接绑定。</summary>
    public ObservableCollection<StockInfo> Items { get; }

    public bool Contains(string fullCode) => Items.Any(s => s.FullCode == fullCode);

    public void Add(StockInfo s)
    {
        if (Contains(s.FullCode)) return;
        _cache.AddWatch(s);
        Items.Add(s);
    }

    public void Remove(string fullCode)
    {
        var existing = Items.FirstOrDefault(s => s.FullCode == fullCode);
        if (existing == null) return;
        _cache.RemoveWatch(fullCode);
        Items.Remove(existing);
    }

    public void Toggle(StockInfo s)
    {
        if (Contains(s.FullCode)) Remove(s.FullCode);
        else Add(s);
    }
}
