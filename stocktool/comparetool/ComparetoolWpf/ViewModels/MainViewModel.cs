using CommunityToolkit.Mvvm.ComponentModel;
using ComparetoolWpf.Services;

namespace ComparetoolWpf.ViewModels;

/// <summary>主窗体 ViewModel：聚合各功能页面的子 VM。</summary>
public partial class MainViewModel : ObservableObject
{
    public WatchlistViewModel WatchlistVm { get; }
    public SinglePeriodCompareViewModel SingleVm { get; }
    public MultiStockCompareViewModel MultiVm { get; }
    public MetricsViewModel MetricsVm { get; }
    public TrendChartViewModel TrendVm { get; }
    public ScreenerViewModel ScreenerVm { get; }

    public MainViewModel()
    {
        var cache = new ReportCache();
        var data = new StockDataService(cache);
        var watch = new WatchlistService(cache);

        WatchlistVm = new WatchlistViewModel(data, watch);
        SingleVm = new SinglePeriodCompareViewModel(data, watch);
        MultiVm = new MultiStockCompareViewModel(data, watch);
        MetricsVm = new MetricsViewModel(data);
        TrendVm = new TrendChartViewModel(data, watch);
        ScreenerVm = new ScreenerViewModel(new ScreenerService(), watch);
        SettingsVm = new SettingsViewModel();
    }

    public SettingsViewModel SettingsVm { get; }
}
