using System.Net.Http;
using ComparetoolWpf.Models;
using ComparetoolWpf.Services;
using Xunit;

namespace ComparetoolWpf.Tests;

internal class StubSource : IStockDataSource
{
    public string Name { get; }
    public Func<Task<List<StockInfo>>>? OnSearch { get; set; }
    public Func<Task<List<FinancialReport>>>? OnReports { get; set; }
    public int SearchCalls;
    public int ReportCalls;

    public StubSource(string name) { Name = name; }

    public Task<List<StockInfo>> SearchStocksAsync(string keyword, CancellationToken ct = default)
    {
        Interlocked.Increment(ref SearchCalls);
        return OnSearch?.Invoke() ?? Task.FromResult(new List<StockInfo>());
    }

    public Task<List<FinancialReport>> GetReportsAsync(
        StockInfo stock, ReportKind kind, ReportPeriodType periodType = ReportPeriodType.All,
        int pageSize = 20, CancellationToken ct = default)
    {
        Interlocked.Increment(ref ReportCalls);
        return OnReports?.Invoke() ?? Task.FromResult(new List<FinancialReport>());
    }
}

public class SourceDecoratorsTests
{
    [Fact]
    public async Task Retry_SucceedsAfterTransientFailures()
    {
        var inner = new StubSource("A")
        {
            OnSearch = () =>
            {
                throw new HttpRequestException("transient");
            },
        };
        int n = 0;
        inner.OnSearch = () =>
        {
            n++;
            if (n < 3) throw new HttpRequestException("transient #" + n);
            return Task.FromResult(new List<StockInfo> { new("600000", "x", "SH") });
        };
        var retry = new RetryingStockSource(inner, maxAttempts: 3, baseDelayMs: 1);
        var r = await retry.SearchStocksAsync("k");
        Assert.Single(r);
        Assert.Equal(3, n);
    }

    [Fact]
    public async Task Retry_NotSupported_PassThrough()
    {
        var inner = new StubSource("A")
        {
            OnSearch = () => throw new NotSupportedException("nope"),
        };
        var retry = new RetryingStockSource(inner, 5, 1);
        await Assert.ThrowsAsync<NotSupportedException>(() => retry.SearchStocksAsync("k"));
        Assert.Equal(1, inner.SearchCalls);
    }

    [Fact]
    public async Task Retry_NameContainsCount()
    {
        var inner = new StubSource("XYZ");
        var retry = new RetryingStockSource(inner, 4, 1);
        Assert.Contains("XYZ", retry.Name);
        Assert.Contains("4", retry.Name);
        await Task.CompletedTask;
    }

    [Fact]
    public async Task Retry_FailsAfterMaxAttempts_ThrowsLast()
    {
        var inner = new StubSource("A")
        {
            OnSearch = () => throw new HttpRequestException("always"),
        };
        var retry = new RetryingStockSource(inner, 2, 1);
        await Assert.ThrowsAsync<HttpRequestException>(() => retry.SearchStocksAsync("k"));
        Assert.Equal(2, inner.SearchCalls);
    }

    [Fact]
    public async Task Retry_OperationCanceled_WithRequestedToken_Throws()
    {
        var cts = new CancellationTokenSource();
        cts.Cancel();
        var inner = new StubSource("A")
        {
            OnSearch = () => throw new OperationCanceledException(cts.Token),
        };
        var retry = new RetryingStockSource(inner, 3, 1);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => retry.SearchStocksAsync("k", cts.Token));
    }

    [Fact]
    public async Task Composite_FallsBack_OnFirstFailure()
    {
        var a = new StubSource("A") { OnReports = () => throw new HttpRequestException("x") };
        var b = new StubSource("B")
        {
            OnReports = () => Task.FromResult(new List<FinancialReport>
                { new() { Kind = ReportKind.Income } }),
        };
        var comp = new CompositeStockSource(a, b);
        var r = await comp.GetReportsAsync(new StockInfo("1", "n", "SH"), ReportKind.Income);
        Assert.Single(r);
        Assert.Equal(1, a.ReportCalls);
        Assert.Equal(1, b.ReportCalls);
        Assert.Contains("B", comp.Name);
    }

    [Fact]
    public async Task Composite_AllFail_ThrowsInvalidOperation()
    {
        var a = new StubSource("A") { OnSearch = () => throw new HttpRequestException("a") };
        var b = new StubSource("B") { OnSearch = () => throw new HttpRequestException("b") };
        var comp = new CompositeStockSource(a, b);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => comp.SearchStocksAsync("k"));
        Assert.IsType<HttpRequestException>(ex.InnerException);
    }

    [Fact]
    public void Composite_RequiresAtLeastOneSource()
    {
        Assert.Throws<ArgumentException>(() => new CompositeStockSource(Array.Empty<IStockDataSource>()));
    }
}
