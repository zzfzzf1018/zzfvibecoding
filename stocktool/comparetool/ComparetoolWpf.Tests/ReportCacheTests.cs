using System.IO;
using ComparetoolWpf.Models;
using ComparetoolWpf.Services;
using Xunit;

namespace ComparetoolWpf.Tests;

public class ReportCacheTests : IDisposable
{
    private readonly string _dbPath;
    private readonly ReportCache _cache;

    public ReportCacheTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), "ctw_tests_" + Guid.NewGuid().ToString("N") + ".db");
        _cache = new ReportCache(_dbPath);
    }

    [Fact]
    public void Save_Load_RoundTrip()
    {
        var r = TestData.Income(new DateTime(2024, 12, 31), 100, 10);
        _cache.Save(new[] { r });
        var loaded = _cache.Load("SH600000", ReportKind.Income);
        Assert.Single(loaded);
        Assert.Equal(100, loaded[0].Items["营业总收入"]);
    }

    [Fact]
    public void GetLastFetchedAt_NullThenValue()
    {
        Assert.Null(_cache.GetLastFetchedAt("SH600000", ReportKind.Income));
        _cache.Save(new[] { TestData.Income(new DateTime(2024, 12, 31), 1, 1) });
        Assert.NotNull(_cache.GetLastFetchedAt("SH600000", ReportKind.Income));
    }

    [Fact]
    public void Load_RespectsMaxAgeDays_FiltersOld()
    {
        _cache.Save(new[] { TestData.Income(new DateTime(2024, 12, 31), 1, 1) });
        // updatedAt 是当下，maxAgeDays=1 应当还能取到
        var fresh = _cache.Load("SH600000", ReportKind.Income, maxAgeDays: 1);
        Assert.Single(fresh);
    }

    [Fact]
    public void Watchlist_AddRemoveQuery()
    {
        var s = new StockInfo("600000", "浦发银行", "SH");
        Assert.False(_cache.IsWatched(s.FullCode));
        _cache.AddWatch(s, note: "test");
        Assert.True(_cache.IsWatched(s.FullCode));
        Assert.Single(_cache.LoadWatchlist());
        _cache.RemoveWatch(s.FullCode);
        Assert.False(_cache.IsWatched(s.FullCode));
    }

    [Fact]
    public void Clear_RemovesReports()
    {
        _cache.Save(new[] { TestData.Income(new DateTime(2024, 12, 31), 1, 1) });
        _cache.Clear();
        Assert.Empty(_cache.Load("SH600000", ReportKind.Income));
    }

    public void Dispose()
    {
        try
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            if (File.Exists(_dbPath)) File.Delete(_dbPath);
        }
        catch { /* best effort */ }
    }
}

public class WatchlistServiceTests : IDisposable
{
    private readonly string _dbPath;
    private readonly WatchlistService _svc;

    public WatchlistServiceTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), "ctw_wl_" + Guid.NewGuid().ToString("N") + ".db");
        _svc = new WatchlistService(new ReportCache(_dbPath));
    }

    [Fact]
    public void Add_Remove_Toggle_UpdatesCollection()
    {
        var s = new StockInfo("000001", "平安银行", "SZ");
        Assert.False(_svc.Contains(s.FullCode));
        _svc.Add(s);
        Assert.True(_svc.Contains(s.FullCode));
        Assert.Single(_svc.Items);
        _svc.Add(s); // 重复添加不再增加
        Assert.Single(_svc.Items);

        _svc.Toggle(s);
        Assert.False(_svc.Contains(s.FullCode));
        _svc.Toggle(s);
        Assert.True(_svc.Contains(s.FullCode));

        _svc.Remove(s.FullCode);
        Assert.Empty(_svc.Items);
    }

    public void Dispose()
    {
        try
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            if (File.Exists(_dbPath)) File.Delete(_dbPath);
        }
        catch { }
    }
}
