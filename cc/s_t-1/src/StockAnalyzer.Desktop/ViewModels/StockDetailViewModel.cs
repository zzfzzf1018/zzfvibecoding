using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using StockAnalyzer.Core.Analytics;
using StockAnalyzer.Core.Models;
using StockAnalyzer.Core.Services;
using StockAnalyzer.Desktop.Infrastructure;

namespace StockAnalyzer.Desktop.ViewModels;

/// <summary>个股详情：基本信息 + 历史分位 + 估值通道。</summary>
public sealed partial class StockDetailViewModel : ObservableObject
{
    private readonly StockService _stockService;
    private readonly ILogger<StockDetailViewModel> _logger;
    private CancellationTokenSource? _loadCts;

    [ObservableProperty]
    private StockInfo? _stock;

    [ObservableProperty]
    private StockQuote? _quote;

    [ObservableProperty]
    private ValuationSeries? _series;

    [ObservableProperty]
    private ValuationChannel? _channel;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string _statusMessage = "请从左侧搜索并选择一只股票。";

    [ObservableProperty]
    private string? _qualityNote;

    [ObservableProperty]
    private EnumOption<ValuationMetric> _selectedChannelMetric;

    [ObservableProperty]
    private EnumOption<LookbackWindow> _selectedChannelWindow;

    [ObservableProperty]
    private EnumOption<ValuationMetric> _selectedHistoryMetric;

    [ObservableProperty]
    private EnumOption<LookbackWindow> _selectedHistoryWindow;

    public StockDetailViewModel(StockService stockService, ILogger<StockDetailViewModel> logger)
    {
        _stockService = stockService;
        _logger = logger;

        MetricOptions = new[]
        {
            new EnumOption<ValuationMetric>(ValuationMetric.PeTtm, "市盈率 PE(TTM)"),
            new EnumOption<ValuationMetric>(ValuationMetric.Pb, "市净率 PB")
        };

        WindowOptions = LookbackWindowExtensions.All
            .Select(w => new EnumOption<LookbackWindow>(w, w.ToDisplayName()))
            .ToArray();

        _selectedChannelMetric = MetricOptions[0];
        _selectedChannelWindow = WindowOptions[1];
        _selectedHistoryMetric = MetricOptions[0];
        _selectedHistoryWindow = WindowOptions[3];
    }

    public IReadOnlyList<EnumOption<ValuationMetric>> MetricOptions { get; }

    public IReadOnlyList<EnumOption<LookbackWindow>> WindowOptions { get; }

    public ObservableCollection<MetricItem> BasicMetrics { get; } = new();

    public ObservableCollection<PercentileRowViewModel> PePercentiles { get; } = new();

    public ObservableCollection<PercentileRowViewModel> PbPercentiles { get; } = new();

    // ---------------- 头部展示 ----------------

    public bool HasStock => Stock is not null;

    public string HeaderName => Quote?.Name is { Length: > 0 } name ? name : Stock?.Name ?? "--";

    public string HeaderCode => Stock is null ? "--" : $"{Stock.Code} · {Stock.Market.ToDisplayName()}";

    public string HeaderPrice => Formatting.Number(Quote?.Price);

    public string HeaderChange =>
        $"{Formatting.Signed(Quote?.Change)}  {Formatting.SignedPercent(Quote?.ChangePercent)}";

    public double? HeaderTrend => Quote?.ChangePercent;

    public string CapturedAtText => Quote is null ? "--" : Formatting.DateTimeText(Quote.CapturedAt);

    // ---------------- 加载 ----------------

    /// <summary>加载指定股票的全部分析数据。</summary>
    public async Task LoadAsync(StockInfo stock, bool forceRefresh = false)
    {
        _loadCts?.Cancel();
        _loadCts?.Dispose();
        _loadCts = new CancellationTokenSource();
        CancellationToken token = _loadCts.Token;

        Stock = stock;
        IsLoading = true;
        StatusMessage = "正在加载行情…";
        NotifyHeaderChanged();

        try
        {
            Quote = await _stockService.GetQuoteAsync(stock, forceRefresh, token);
            BuildBasicMetrics();
            NotifyHeaderChanged();

            var progress = new ImmediateProgress<string>(message => StatusMessage = message);

            ValuationSeries series = await _stockService.GetValuationSeriesAsync(
                stock, years: 10, forceRefresh: forceRefresh, progress: progress, cancellationToken: token);

            if (token.IsCancellationRequested)
            {
                return;
            }

            Series = series;
            QualityNote = series.Points.Count == 0
                ? "未获取到历史数据，无法计算分位与估值通道。"
                : series.QualityDescription;

            RebuildPercentiles();
            RebuildChannel();

            StatusMessage = series.Points.Count == 0
                ? "历史数据为空。"
                : $"已加载 {series.Points.Count} 个交易日数据（{Formatting.Date(series.Points[0].Date)} 至 {Formatting.Date(series.Points[^1].Date)}）。";
        }
        catch (OperationCanceledException)
        {
            // 用户切换了标的，忽略
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "加载个股数据失败：{Code}", stock.Code);
            StatusMessage = $"加载失败：{ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        if (Stock is not null)
        {
            await LoadAsync(Stock, forceRefresh: true);
        }
    }

    private void BuildBasicMetrics()
    {
        BasicMetrics.Clear();

        if (Quote is null)
        {
            return;
        }

        string currency = Quote.Market == MarketType.HongKong ? "港元" : "元";

        BasicMetrics.Add(new MetricItem("最新价", Formatting.Number(Quote.Price), Quote.ChangePercent));
        BasicMetrics.Add(new MetricItem("涨跌幅", Formatting.SignedPercent(Quote.ChangePercent), Quote.ChangePercent));
        BasicMetrics.Add(new MetricItem("今开", Formatting.Number(Quote.Open)));
        BasicMetrics.Add(new MetricItem("昨收", Formatting.Number(Quote.PreviousClose)));
        BasicMetrics.Add(new MetricItem("最高", Formatting.Number(Quote.High)));
        BasicMetrics.Add(new MetricItem("最低", Formatting.Number(Quote.Low)));
        BasicMetrics.Add(new MetricItem("振幅", Formatting.Percent(Quote.Amplitude)));
        BasicMetrics.Add(new MetricItem("换手率", Formatting.Percent(Quote.TurnoverRate)));
        BasicMetrics.Add(new MetricItem("成交额", Formatting.MoneyCompact(Quote.Turnover, currency)));
        BasicMetrics.Add(new MetricItem("总市值", Formatting.MoneyCompact(Quote.TotalMarketCap, currency)));
        BasicMetrics.Add(new MetricItem("流通市值", Formatting.MoneyCompact(Quote.CirculatingMarketCap, currency)));
        BasicMetrics.Add(new MetricItem("总股本", Formatting.MoneyCompact(Quote.TotalShares, "股")));
        BasicMetrics.Add(new MetricItem("市盈率 TTM", Formatting.Multiple(Quote.PeTtm), tooltip: "滚动最近 12 个月净利润口径；历史分位与估值通道均基于此口径"));
        BasicMetrics.Add(new MetricItem("市盈率 静态", Formatting.Multiple(Quote.PeStatic), tooltip: "基于最近一期年报"));
        BasicMetrics.Add(new MetricItem("市盈率 动态", Formatting.Multiple(Quote.PeDynamic), tooltip: "最新报告期业绩年化推算"));
        BasicMetrics.Add(new MetricItem("市净率 PB", Formatting.Multiple(Quote.Pb)));
        BasicMetrics.Add(new MetricItem("每股净资产", Formatting.Number(Quote.BookValuePerShare), tooltip: "由最新价 ÷ PB 反算"));
    }

    private void RebuildPercentiles()
    {
        PePercentiles.Clear();
        PbPercentiles.Clear();

        if (Series is null || Series.Points.Count == 0)
        {
            return;
        }

        foreach (PercentileResult result in ValuationAnalyzer.CalculateAllWindows(Series, ValuationMetric.PeTtm))
        {
            PePercentiles.Add(new PercentileRowViewModel(result));
        }

        foreach (PercentileResult result in ValuationAnalyzer.CalculateAllWindows(Series, ValuationMetric.Pb))
        {
            PbPercentiles.Add(new PercentileRowViewModel(result));
        }
    }

    private void RebuildChannel()
    {
        if (Series is null || Stock is null || Series.Points.Count == 0)
        {
            Channel = null;
            return;
        }

        Channel = ValuationAnalyzer.BuildChannel(
            Stock,
            Series,
            SelectedChannelMetric.Value,
            SelectedChannelWindow.Value);
    }

    partial void OnSelectedChannelMetricChanged(EnumOption<ValuationMetric> value) => RebuildChannel();

    partial void OnSelectedChannelWindowChanged(EnumOption<LookbackWindow> value) => RebuildChannel();

    partial void OnQuoteChanged(StockQuote? value) => NotifyHeaderChanged();

    private void NotifyHeaderChanged()
    {
        OnPropertyChanged(nameof(HasStock));
        OnPropertyChanged(nameof(HeaderName));
        OnPropertyChanged(nameof(HeaderCode));
        OnPropertyChanged(nameof(HeaderPrice));
        OnPropertyChanged(nameof(HeaderChange));
        OnPropertyChanged(nameof(HeaderTrend));
        OnPropertyChanged(nameof(CapturedAtText));
    }
}
