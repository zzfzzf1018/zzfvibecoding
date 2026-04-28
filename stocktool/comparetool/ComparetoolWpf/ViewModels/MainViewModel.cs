using CommunityToolkit.Mvvm.ComponentModel;
using ComparetoolWpf.Services;

namespace ComparetoolWpf.ViewModels;

/// <summary>主窗体 ViewModel：聚合各功能页面的子 VM。</summary>
public partial class MainViewModel : ObservableObject
{
    public SinglePeriodCompareViewModel SingleVm { get; }
    public MultiStockCompareViewModel MultiVm { get; }
    public MetricsViewModel MetricsVm { get; }
    public TrendChartViewModel TrendVm { get; }

    public MainViewModel()
    {
        var api = new EastMoneyService();
        var cache = new ReportCache();
        var data = new StockDataService(api, cache);

        SingleVm = new SinglePeriodCompareViewModel(data);
        MultiVm = new MultiStockCompareViewModel(data);
        MetricsVm = new MetricsViewModel(data);
        TrendVm = new TrendChartViewModel(data);
    }
}
