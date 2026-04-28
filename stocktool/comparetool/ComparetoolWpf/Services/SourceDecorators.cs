using System.Net.Http;
using ComparetoolWpf.Models;

namespace ComparetoolWpf.Services;

/// <summary>
/// 装饰器：对内部 <see cref="IStockDataSource"/> 调用做指数退避重试。
/// 仅对网络相关异常（HttpRequestException / TaskCanceledException）重试；
/// <see cref="NotSupportedException"/> 等业务异常直接透传以触发降级。
/// </summary>
public class RetryingStockSource : IStockDataSource
{
    private readonly IStockDataSource _inner;
    private readonly int _maxAttempts;
    private readonly TimeSpan _baseDelay;

    public string Name => $"{_inner.Name}(重试×{_maxAttempts})";

    public RetryingStockSource(IStockDataSource inner, int maxAttempts = 3, int baseDelayMs = 500)
    {
        _inner = inner;
        _maxAttempts = Math.Max(1, maxAttempts);
        _baseDelay = TimeSpan.FromMilliseconds(baseDelayMs);
    }

    public Task<List<StockInfo>> SearchStocksAsync(string keyword, CancellationToken ct = default)
        => RunAsync($"Search[{_inner.Name}]({keyword})",
                    () => _inner.SearchStocksAsync(keyword, ct), ct);

    public Task<List<FinancialReport>> GetReportsAsync(
        StockInfo stock, ReportKind kind, ReportPeriodType periodType = ReportPeriodType.All,
        int pageSize = 20, CancellationToken ct = default)
        => RunAsync($"Reports[{_inner.Name}]({stock.FullCode},{kind})",
                    () => _inner.GetReportsAsync(stock, kind, periodType, pageSize, ct), ct);

    private async Task<T> RunAsync<T>(string op, Func<Task<T>> action, CancellationToken ct)
    {
        Exception? last = null;
        for (int attempt = 1; attempt <= _maxAttempts; attempt++)
        {
            try
            {
                return await action();
            }
            catch (NotSupportedException) { throw; }      // 业务"不支持"，立刻降级
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
            catch (Exception ex) when (IsTransient(ex))
            {
                last = ex;
                if (attempt == _maxAttempts) break;
                var delay = TimeSpan.FromMilliseconds(_baseDelay.TotalMilliseconds * Math.Pow(3, attempt - 1));
                Logger.Warn($"{op} 失败({attempt}/{_maxAttempts}) {ex.GetType().Name}: {ex.Message}, {delay.TotalMilliseconds:F0}ms 后重试");
                await Task.Delay(delay, ct);
            }
        }
        throw last ?? new Exception($"{op} 失败");
    }

    private static bool IsTransient(Exception ex)
        => ex is HttpRequestException
           || ex is TaskCanceledException
           || ex is TimeoutException
           || ex is System.IO.IOException;
}

/// <summary>
/// 装饰器：按顺序尝试一组数据源；当前一个抛异常时自动降级到下一个。
/// <see cref="Name"/> 返回当前最近成功使用的源名称（用于 UI 显示）。
/// </summary>
public class CompositeStockSource : IStockDataSource
{
    private readonly IReadOnlyList<IStockDataSource> _sources;
    private string _lastUsed;

    public string Name => $"自动({_lastUsed})";

    public CompositeStockSource(params IStockDataSource[] sources)
    {
        if (sources == null || sources.Length == 0)
            throw new ArgumentException("至少提供一个数据源", nameof(sources));
        _sources = sources;
        _lastUsed = sources[0].Name;
    }

    public Task<List<StockInfo>> SearchStocksAsync(string keyword, CancellationToken ct = default)
        => TryAllAsync("Search", s => s.SearchStocksAsync(keyword, ct));

    public Task<List<FinancialReport>> GetReportsAsync(
        StockInfo stock, ReportKind kind, ReportPeriodType periodType = ReportPeriodType.All,
        int pageSize = 20, CancellationToken ct = default)
        => TryAllAsync($"Reports[{stock.FullCode},{kind}]",
                       s => s.GetReportsAsync(stock, kind, periodType, pageSize, ct));

    private async Task<T> TryAllAsync<T>(string op, Func<IStockDataSource, Task<T>> action)
    {
        Exception? last = null;
        foreach (var src in _sources)
        {
            try
            {
                var r = await action(src);
                _lastUsed = src.Name;
                return r;
            }
            catch (Exception ex)
            {
                last = ex;
                Logger.Warn($"{op} via {src.Name} 失败 → 降级: {ex.Message}");
            }
        }
        throw new InvalidOperationException($"{op} 所有数据源均失败。", last);
    }
}
