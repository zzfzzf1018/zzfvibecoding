using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using StockAnalyzer.Core.Models;
using StockAnalyzer.Core.Services;
using StockAnalyzer.Desktop.Infrastructure;

namespace StockAnalyzer.Desktop.ViewModels;

/// <summary>主窗口视图模型：检索、自选股与全局状态。</summary>
public sealed partial class MainViewModel : ObservableObject
{
    private readonly StockService _stockService;
    private readonly ILogger<MainViewModel> _logger;
    private CancellationTokenSource? _searchCts;

    [ObservableProperty]
    private string _searchKeyword = string.Empty;

    [ObservableProperty]
    private EnumOption<MarketGroup> _selectedMarketGroup;

    [ObservableProperty]
    private StockInfo? _selectedSearchResult;

    [ObservableProperty]
    private WatchlistRowViewModel? _selectedWatchlistRow;

    [ObservableProperty]
    private string _statusMessage = "就绪";

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string _storageSummary = "--";

    [ObservableProperty]
    private string _dataSourceName = string.Empty;

    public MainViewModel(
        StockService stockService,
        StockDetailViewModel detail,
        ILogger<MainViewModel> logger)
    {
        _stockService = stockService;
        _logger = logger;
        Detail = detail;

        MarketGroupOptions = new[]
        {
            new EnumOption<MarketGroup>(MarketGroup.All, "全部"),
            new EnumOption<MarketGroup>(MarketGroup.AShare, "A 股"),
            new EnumOption<MarketGroup>(MarketGroup.HongKong, "港股")
        };

        _selectedMarketGroup = MarketGroupOptions[0];
        _dataSourceName = stockService.DataSourceName;
    }

    public StockDetailViewModel Detail { get; }

    public IReadOnlyList<EnumOption<MarketGroup>> MarketGroupOptions { get; }

    public ObservableCollection<StockInfo> SearchResults { get; } = new();

    public ObservableCollection<WatchlistRowViewModel> Watchlist { get; } = new();

    // ------------------------------------------------------------------
    // 初始化
    // ------------------------------------------------------------------

    public async Task InitializeAsync()
    {
        IsBusy = true;

        try
        {
            StatusMessage = "正在初始化本地数据库…";
            await _stockService.InitializeAsync();

            await LoadWatchlistAsync();
            await RefreshStorageSummaryAsync();
            await RefreshWatchlistQuotesAsync();

            StatusMessage = "就绪（搜索支持代码 / 名称 / 拼音首字母）";

            // 全量列表仅用于离线检索加速，放到后台尝试，失败不影响使用
            _ = SyncStockListInBackgroundAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "初始化失败。");
            StatusMessage = $"初始化失败：{ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    // ------------------------------------------------------------------
    // 检索
    // ------------------------------------------------------------------

    partial void OnSearchKeywordChanged(string value) => _ = SearchAsync(value);

    partial void OnSelectedMarketGroupChanged(EnumOption<MarketGroup> value) => _ = SearchAsync(SearchKeyword);

    /// <summary>带 250ms 去抖的增量检索。</summary>
    private async Task SearchAsync(string keyword)
    {
        _searchCts?.Cancel();
        _searchCts?.Dispose();
        _searchCts = new CancellationTokenSource();
        CancellationToken token = _searchCts.Token;

        keyword = keyword?.Trim() ?? string.Empty;

        if (keyword.Length == 0)
        {
            SearchResults.Clear();
            return;
        }

        try
        {
            await Task.Delay(250, token);

            IReadOnlyList<StockInfo> results =
                await _stockService.SearchAsync(keyword, SelectedMarketGroup.Value, 60, token);

            if (token.IsCancellationRequested)
            {
                return;
            }

            SearchResults.Clear();
            foreach (StockInfo stock in results)
            {
                SearchResults.Add(stock);
            }

            StatusMessage = results.Count == 0
                ? $"未找到匹配「{keyword}」的股票，可尝试同步股票列表。"
                : $"找到 {results.Count} 条结果。";
        }
        catch (OperationCanceledException)
        {
            // 输入过程中的正常取消
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "检索失败。");
            StatusMessage = $"检索失败：{ex.Message}";
        }
    }

    [RelayCommand]
    private async Task SyncStockListAsync()
    {
        IsBusy = true;

        try
        {
            var progress = new ImmediateProgress<string>(message => StatusMessage = message);
            await _stockService.EnsureStockListAsync(forceRefresh: true, progress: progress);
            await RefreshStorageSummaryAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "同步股票列表失败。");
            StatusMessage = $"同步失败：{ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task SyncStockListInBackgroundAsync()
    {
        try
        {
            bool synced = await _stockService.EnsureStockListAsync(forceRefresh: false);

            if (synced)
            {
                await RefreshStorageSummaryAsync();
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "后台同步股票列表失败。");
        }
    }

    // ------------------------------------------------------------------
    // 选择与自选股
    // ------------------------------------------------------------------

    partial void OnSelectedSearchResultChanged(StockInfo? value)
    {
        if (value is not null)
        {
            _ = LoadDetailAsync(value);
        }
    }

    partial void OnSelectedWatchlistRowChanged(WatchlistRowViewModel? value)
    {
        if (value is not null)
        {
            _ = LoadDetailAsync(value.ToStockInfo());
        }
    }

    private async Task LoadDetailAsync(StockInfo stock)
    {
        await Detail.LoadAsync(stock);
        await RefreshStorageSummaryAsync();
    }

    [RelayCommand]
    private async Task AddToWatchlistAsync(StockInfo? stock)
    {
        stock ??= SelectedSearchResult ?? Detail.Stock;

        if (stock is null)
        {
            StatusMessage = "请先选择一只股票。";
            return;
        }

        await _stockService.AddToWatchlistAsync(stock);
        await LoadWatchlistAsync();
        await RefreshWatchlistQuotesAsync();
        await RefreshStorageSummaryAsync();

        StatusMessage = $"已加入自选：{stock.Code} {stock.Name}";
    }

    [RelayCommand]
    private async Task RemoveFromWatchlistAsync(WatchlistRowViewModel? row)
    {
        row ??= SelectedWatchlistRow;

        if (row is null)
        {
            return;
        }

        MessageBoxResult confirm = MessageBox.Show(
            $"确认从自选中移除 {row.Code} {row.Name}？",
            "移除自选",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (confirm != MessageBoxResult.Yes)
        {
            return;
        }

        await _stockService.RemoveFromWatchlistAsync(row.Market, row.Code);
        await LoadWatchlistAsync();
        await RefreshStorageSummaryAsync();

        StatusMessage = $"已移除：{row.Code}";
    }

    private async Task LoadWatchlistAsync()
    {
        IReadOnlyList<WatchlistItem> items = await _stockService.GetWatchlistAsync();

        Watchlist.Clear();
        foreach (WatchlistItem item in items)
        {
            Watchlist.Add(new WatchlistRowViewModel(item));
        }
    }

    [RelayCommand]
    private async Task RefreshWatchlistQuotesAsync()
    {
        if (Watchlist.Count == 0)
        {
            return;
        }

        try
        {
            var stocks = Watchlist.Select(w => w.ToStockInfo()).ToList();
            IReadOnlyList<StockQuote> quotes = await _stockService.GetQuotesAsync(stocks);

            var map = quotes.ToDictionary(q => StockInfo.BuildKey(q.Market, q.Code));

            foreach (WatchlistRowViewModel row in Watchlist)
            {
                if (map.TryGetValue(StockInfo.BuildKey(row.Market, row.Code), out StockQuote? quote))
                {
                    row.Quote = quote;
                }
            }

            StatusMessage = $"自选股行情已刷新（{DateTime.Now:HH:mm:ss}）。";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "刷新自选股行情失败。");
            StatusMessage = $"刷新失败：{ex.Message}";
        }
    }

    // ------------------------------------------------------------------
    // 本地存储
    // ------------------------------------------------------------------

    private async Task RefreshStorageSummaryAsync()
    {
        IReadOnlyDictionary<string, long> stats = await _stockService.Repository.GetStorageStatisticsAsync();
        StorageSummary = string.Join("   ", stats.Select(kv => $"{kv.Key} {kv.Value:N0}"));
    }

    [RelayCommand]
    private async Task ClearCacheAsync()
    {
        MessageBoxResult confirm = MessageBox.Show(
            "将清空本地缓存的日线与财报数据（自选股与股票列表会保留）。\n下次查询时会重新联网获取，确认继续？",
            "清理本地缓存",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (confirm != MessageBoxResult.Yes)
        {
            return;
        }

        IsBusy = true;

        try
        {
            await _stockService.Repository.ClearHistoryCacheAsync();
            await RefreshStorageSummaryAsync();
            StatusMessage = "本地缓存已清理。";
        }
        finally
        {
            IsBusy = false;
        }
    }
}
