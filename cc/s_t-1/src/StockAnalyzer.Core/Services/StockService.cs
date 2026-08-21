using Microsoft.Extensions.Logging;
using StockAnalyzer.Core.Abstractions;
using StockAnalyzer.Core.Analytics;
using StockAnalyzer.Core.Models;
using StockAnalyzer.Core.Utils;

namespace StockAnalyzer.Core.Services;

/// <summary>
/// 应用服务：统一编排「本地缓存优先 + 按需回源」的取数逻辑。
/// </summary>
public sealed class StockService
{
    private const string StockListStampKeyPrefix = "stocklist";
    private const string BarsStampKeyPrefix = "bars";
    private const string ReportsStampKeyPrefix = "reports";

    /// <summary>股票列表的缓存有效期。</summary>
    private static readonly TimeSpan StockListTtl = TimeSpan.FromDays(3);

    /// <summary>实时快照的缓存有效期。</summary>
    private static readonly TimeSpan QuoteTtl = TimeSpan.FromMinutes(1);

    /// <summary>财报的缓存有效期。</summary>
    private static readonly TimeSpan ReportsTtl = TimeSpan.FromDays(7);

    private readonly IStockRepository _repository;
    private readonly IStockDataSource _dataSource;
    private readonly ILogger<StockService> _logger;

    public StockService(
        IStockRepository repository,
        IStockDataSource dataSource,
        ILogger<StockService> logger)
    {
        _repository = repository;
        _dataSource = dataSource;
        _logger = logger;
    }

    public string DataSourceName => _dataSource.Name;

    public IStockRepository Repository => _repository;

    public Task InitializeAsync(CancellationToken cancellationToken = default) =>
        _repository.InitializeAsync(cancellationToken);

    // ------------------------------------------------------------------
    // 股票列表
    // ------------------------------------------------------------------

    /// <summary>确保本地存在可用的股票列表，必要时回源刷新。</summary>
    /// <remarks>
    /// 全量列表属于“锦上添花”：数据源对该接口限流较严，失败不影响使用（检索会自动走在线接口）。
    /// </remarks>
    /// <returns>本次是否发生了网络同步。</returns>
    public async Task<bool> EnsureStockListAsync(
        bool forceRefresh = false,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default)
    {
        bool needSync = forceRefresh;

        if (!needSync)
        {
            int count = await _repository.GetStockCountAsync(MarketGroup.All, cancellationToken);
            DateTime? stamp = await _repository.GetSyncStampAsync(StockListStampKeyPrefix, cancellationToken);
            needSync = count == 0 || stamp is null || DateTime.Now - stamp.Value > StockListTtl;
        }

        if (!needSync)
        {
            return false;
        }

        progress?.Report("正在同步全市场股票列表…");

        IReadOnlyList<StockInfo> stocks = await _dataSource.GetStockListAsync(MarketGroup.All, cancellationToken);

        if (stocks.Count == 0)
        {
            _logger.LogWarning("股票列表同步返回空结果，保留原有本地数据。");
            progress?.Report("股票列表同步失败，继续使用本地缓存。");
            return false;
        }

        await _repository.ReplaceStocksAsync(stocks, cancellationToken);
        await _repository.SetSyncStampAsync(StockListStampKeyPrefix, DateTime.Now, cancellationToken);

        progress?.Report($"股票列表已更新，共 {stocks.Count} 只。");
        return true;
    }

    /// <summary>
    /// 模糊检索：先查本地（秒回、可离线），再用在线搜索补充本地缺失的标的，
    /// 并把在线结果写回本地，使下次检索无需联网。
    /// </summary>
    public async Task<IReadOnlyList<StockInfo>> SearchAsync(
        string keyword,
        MarketGroup group = MarketGroup.All,
        int maxResults = 50,
        CancellationToken cancellationToken = default)
    {
        keyword = keyword?.Trim() ?? string.Empty;

        if (keyword.Length == 0)
        {
            return Array.Empty<StockInfo>();
        }

        IReadOnlyList<StockInfo> local =
            await _repository.SearchStocksAsync(keyword, group, maxResults, cancellationToken);

        var merged = new List<StockInfo>(local);
        var seen = new HashSet<string>(local.Select(s => s.Key), StringComparer.Ordinal);

        try
        {
            IReadOnlyList<StockInfo> remote =
                await _dataSource.SearchAsync(keyword, group, maxResults, cancellationToken);

            var newcomers = remote.Where(s => seen.Add(s.Key)).ToList();

            if (remote.Count > 0)
            {
                await _repository.UpsertStocksAsync(remote, cancellationToken);
            }

            // 在线结果按匹配度排序，本地已有的保持在前
            merged.AddRange(newcomers);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "在线检索失败，仅返回本地结果。");
        }

        return merged.Take(maxResults).ToList();
    }

    public async Task<StockInfo?> ResolveStockAsync(
        MarketType market,
        string code,
        CancellationToken cancellationToken = default)
    {
        code = SecurityIdHelper.Normalize(code);

        StockInfo? stock = await _repository.GetStockAsync(market, code, cancellationToken);
        if (stock is not null)
        {
            return stock;
        }

        // 本地列表缺失时按代码规则构造，仍可正常取数
        MarketType inferred = market == MarketType.Unknown ? SecurityIdHelper.InferMarket(code) : market;
        return new StockInfo
        {
            Code = code,
            Market = inferred,
            Name = code,
            SecId = SecurityIdHelper.BuildSecId(inferred, code)
        };
    }

    // ------------------------------------------------------------------
    // 自选股
    // ------------------------------------------------------------------

    public Task<IReadOnlyList<WatchlistItem>> GetWatchlistAsync(CancellationToken cancellationToken = default)
        => _repository.GetWatchlistAsync(cancellationToken);

    public Task AddToWatchlistAsync(StockInfo stock, CancellationToken cancellationToken = default)
        => _repository.AddToWatchlistAsync(stock, cancellationToken);

    public Task RemoveFromWatchlistAsync(MarketType market, string code, CancellationToken cancellationToken = default)
        => _repository.RemoveFromWatchlistAsync(market, code, cancellationToken);

    public Task<bool> IsInWatchlistAsync(MarketType market, string code, CancellationToken cancellationToken = default)
        => _repository.IsInWatchlistAsync(market, code, cancellationToken);

    // ------------------------------------------------------------------
    // 行情快照
    // ------------------------------------------------------------------

    /// <summary>获取实时快照。1 分钟内的缓存直接复用；离线时回退到最后一次快照。</summary>
    public async Task<StockQuote?> GetQuoteAsync(
        StockInfo stock,
        bool forceRefresh = false,
        CancellationToken cancellationToken = default)
    {
        StockQuote? cached = await _repository.GetCachedQuoteAsync(stock.Market, stock.Code, cancellationToken);

        if (!forceRefresh && cached is not null && DateTime.Now - cached.CapturedAt < QuoteTtl)
        {
            return cached;
        }

        StockQuote? fresh = await _dataSource.GetQuoteAsync(stock, cancellationToken);

        if (fresh is null)
        {
            _logger.LogWarning("实时快照获取失败，回退到本地缓存：{Code}", stock.Code);
            return cached;
        }

        if (string.IsNullOrWhiteSpace(fresh.Name))
        {
            fresh.Name = stock.Name;
        }

        await _repository.SaveQuoteAsync(fresh, cancellationToken);
        return fresh;
    }

    /// <summary>批量刷新自选股行情。</summary>
    public async Task<IReadOnlyList<StockQuote>> GetQuotesAsync(
        IReadOnlyList<StockInfo> stocks,
        CancellationToken cancellationToken = default)
    {
        if (stocks.Count == 0)
        {
            return Array.Empty<StockQuote>();
        }

        IReadOnlyList<StockQuote> quotes = await _dataSource.GetQuotesAsync(stocks, cancellationToken);

        foreach (StockQuote quote in quotes)
        {
            await _repository.SaveQuoteAsync(quote, cancellationToken);
        }

        return quotes;
    }

    // ------------------------------------------------------------------
    // 估值序列
    // ------------------------------------------------------------------

    /// <summary>
    /// 构建逐日估值序列：先确保本地日线与财报覆盖所需区间，再在本地完成计算。
    /// </summary>
    /// <param name="years">需要覆盖的年数，通常传 10 以支撑全部窗口。</param>
    public async Task<ValuationSeries> GetValuationSeriesAsync(
        StockInfo stock,
        int years = 10,
        bool forceRefresh = false,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default)
    {
        DateTime end = DateTime.Today;
        DateTime start = end.AddYears(-years).AddDays(-30);

        progress?.Report("正在准备日线数据…");
        await EnsureDailyBarsAsync(stock, start, end, forceRefresh, cancellationToken);

        progress?.Report("正在准备财报数据…");
        await EnsureFinancialReportsAsync(stock, forceRefresh, cancellationToken);

        IReadOnlyList<DailyBar> bars =
            await _repository.GetDailyBarsAsync(stock.Market, stock.Code, start, end, cancellationToken);

        IReadOnlyList<FinancialReport> reports =
            await _repository.GetFinancialReportsAsync(stock.Market, stock.Code, cancellationToken);

        StockQuote? quote = await _repository.GetCachedQuoteAsync(stock.Market, stock.Code, cancellationToken);

        progress?.Report("正在计算估值序列…");
        return ValuationSeriesBuilder.Build(stock, bars, reports, quote);
    }

    private async Task EnsureDailyBarsAsync(
        StockInfo stock,
        DateTime start,
        DateTime end,
        bool forceRefresh,
        CancellationToken cancellationToken)
    {
        string stampKey = $"{BarsStampKeyPrefix}:{stock.Market}:{stock.Code}";

        DateTime? firstCached = await _repository.GetFirstBarDateAsync(stock.Market, stock.Code, cancellationToken);
        DateTime? lastCached = await _repository.GetLastBarDateAsync(stock.Market, stock.Code, cancellationToken);
        DateTime? stamp = await _repository.GetSyncStampAsync(stampKey, cancellationToken);

        bool missingHistory = firstCached is null || firstCached.Value > start.AddDays(7);
        bool staleTail = lastCached is null || lastCached.Value.Date < PreviousTradingDay(end);
        bool staleStamp = stamp is null || DateTime.Now - stamp.Value > TimeSpan.FromHours(6);

        if (!forceRefresh && !missingHistory && !staleTail && !staleStamp)
        {
            return;
        }

        // 缺历史时整段重取，仅缺尾部时做增量拉取
        DateTime fetchStart = forceRefresh || missingHistory
            ? start
            : lastCached!.Value.AddDays(-5);

        IReadOnlyList<DailyBar> bars =
            await _dataSource.GetDailyHistoryAsync(stock, fetchStart, end, cancellationToken);

        if (bars.Count == 0)
        {
            _logger.LogWarning("日线数据为空：{Code}", stock.Code);
            return;
        }

        await _repository.SaveDailyBarsAsync(bars, cancellationToken);
        await _repository.SetSyncStampAsync(stampKey, DateTime.Now, cancellationToken);
    }

    private async Task EnsureFinancialReportsAsync(
        StockInfo stock,
        bool forceRefresh,
        CancellationToken cancellationToken)
    {
        string stampKey = $"{ReportsStampKeyPrefix}:{stock.Market}:{stock.Code}";
        DateTime? stamp = await _repository.GetSyncStampAsync(stampKey, cancellationToken);

        if (!forceRefresh && stamp is not null && DateTime.Now - stamp.Value <= ReportsTtl)
        {
            return;
        }

        IReadOnlyList<FinancialReport> reports =
            await _dataSource.GetFinancialReportsAsync(stock, cancellationToken);

        if (reports.Count > 0)
        {
            await _repository.SaveFinancialReportsAsync(reports, cancellationToken);
        }

        // 即使返回空也记录时间戳，避免每次打开都重复请求不支持的市场
        await _repository.SetSyncStampAsync(stampKey, DateTime.Now, cancellationToken);
    }

    private static DateTime PreviousTradingDay(DateTime day)
    {
        // 粗略处理：仅跳过周末，法定节假日由「数据源返回为空」自然兜底
        DateTime candidate = day.DayOfWeek switch
        {
            DayOfWeek.Sunday => day.AddDays(-2),
            DayOfWeek.Saturday => day.AddDays(-1),
            _ => day
        };

        // 收盘前打开软件时，当日 K 线尚未生成，向前多留一天
        if (candidate.Date == DateTime.Today && DateTime.Now.Hour < 16)
        {
            candidate = candidate.AddDays(-1);
            candidate = candidate.DayOfWeek switch
            {
                DayOfWeek.Sunday => candidate.AddDays(-2),
                DayOfWeek.Saturday => candidate.AddDays(-1),
                _ => candidate
            };
        }

        return candidate.Date;
    }
}
