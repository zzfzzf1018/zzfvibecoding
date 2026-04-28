using System.IO;
using ComparetoolWpf.Models;
using ComparetoolWpf.Services;
using Xunit;

namespace ComparetoolWpf.Tests;

[Collection("AppSettings")]
public class StockDataServiceTests : IDisposable
{
    private readonly string _dbPath;
    private readonly ReportCache _cache;
    private readonly StockDataService _svc;
    private readonly AppSettings _backup;

    public StockDataServiceTests()
    {
        _backup = AppSettings.Current;
        _dbPath = Path.Combine(Path.GetTempPath(), "ctw_sds_" + Guid.NewGuid().ToString("N") + ".db");
        _cache = new ReportCache(_dbPath);
        _svc = new StockDataService(_cache);
    }

    [Fact]
    public async Task GetReportsAsync_UsesCache_WhenFreshAndEnoughPeriods()
    {
        var stock = new StockInfo("600000", "x", "SH");
        // 写入 4 期"近 365 天内"的数据
        var reports = new[]
        {
            TestData.Income(DateTime.Today.AddDays(-30), 1, 1),
            TestData.Income(DateTime.Today.AddDays(-120), 2, 2),
            TestData.Income(DateTime.Today.AddDays(-200), 3, 3),
            TestData.Income(DateTime.Today.AddDays(-300), 4, 4),
        };
        _cache.Save(reports);

        // 让 DataSourceFactory 指向一个会抛异常的源 → 若被调用就会失败
        var failing = new StubSource("FAIL")
        {
            OnReports = () => throw new InvalidOperationException("should not be called"),
        };
        InjectSource(failing);
        try
        {
            var got = await _svc.GetReportsAsync(stock, ReportKind.Income, minPeriods: 4);
            Assert.Equal(4, got.Count);
        }
        finally { RestoreSource(); }
    }

    [Fact]
    public async Task GetReportsAsync_ForceRefresh_BypassesCache_AndSaves()
    {
        var stock = new StockInfo("600000", "x", "SH");
        var fresh = new List<FinancialReport>
        {
            TestData.Income(new DateTime(2024, 12, 31), 11, 11),
            TestData.Income(new DateTime(2023, 12, 31), 10, 10),
        };
        var src = new StubSource("OK") { OnReports = () => Task.FromResult(fresh) };
        InjectSource(src);
        try
        {
            var got = await _svc.GetReportsAsync(stock, ReportKind.Income, forceRefresh: true);
            Assert.Equal(2, got.Count);
            Assert.Equal(1, src.ReportCalls);
            Assert.NotEmpty(_cache.Load(stock.FullCode, ReportKind.Income));
        }
        finally { RestoreSource(); }
    }

    [Theory]
    [InlineData(ReportPeriodType.Annual, 12, 31, true)]
    [InlineData(ReportPeriodType.SemiAnnual, 6, 30, true)]
    [InlineData(ReportPeriodType.Q1, 3, 31, true)]
    [InlineData(ReportPeriodType.Q3, 9, 30, true)]
    [InlineData(ReportPeriodType.Annual, 6, 30, false)]
    public async Task GetReportsAsync_FiltersByPeriod(ReportPeriodType pt, int m, int d, bool expectMatch)
    {
        var stock = new StockInfo("600000", "x", "SH");
        var src = new StubSource("OK")
        {
            OnReports = () => Task.FromResult(new List<FinancialReport>
            {
                TestData.Income(new DateTime(2024, m, d), 1, 1),
                TestData.Income(new DateTime(2023, 12, 31), 1, 1),
            }),
        };
        InjectSource(src);
        try
        {
            var got = await _svc.GetReportsAsync(stock, ReportKind.Income, pt, forceRefresh: true);
            if (expectMatch) Assert.Contains(got, r => r.ReportDate == new DateTime(2024, m, d));
            else Assert.DoesNotContain(got, r => r.ReportDate == new DateTime(2024, m, d));
        }
        finally { RestoreSource(); }
    }

    [Fact]
    public void ClearCache_DelegatesToCache()
    {
        _cache.Save(new[] { TestData.Income(new DateTime(2024, 12, 31), 1, 1) });
        _svc.ClearCache();
        Assert.Empty(_cache.Load("SH600000", ReportKind.Income));
    }

    [Fact]
    public async Task SearchStocksAsync_GoesThroughSource()
    {
        var src = new StubSource("OK")
        {
            OnSearch = () => Task.FromResult(new List<StockInfo> { new("000001", "n", "SZ") }),
        };
        InjectSource(src);
        try
        {
            var r = await _svc.SearchStocksAsync("k");
            Assert.Single(r);
            Assert.Equal(1, src.SearchCalls);
        }
        finally { RestoreSource(); }
    }

    private static void InjectSource(IStockDataSource src)
    {
        var fld = typeof(DataSourceFactory).GetField("_current",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;
        fld.SetValue(null, src);
    }

    private void RestoreSource()
    {
        _backup.Save();
        DataSourceFactory.RefreshFromSettings();
    }

    public void Dispose()
    {
        try
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            if (File.Exists(_dbPath)) File.Delete(_dbPath);
        }
        catch { }
        RestoreSource();
    }
}

[CollectionDefinition("AppSettings")]
public class AppSettingsCollection : ICollectionFixture<object> { }
