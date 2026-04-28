using CommunityToolkit.Mvvm.ComponentModel;
using ComparetoolWpf.Services;

namespace ComparetoolWpf.ViewModels;

/// <summary>主窗体 ViewModel：聚合两个功能页面的子 VM。</summary>
public partial class MainViewModel : ObservableObject
{
    public SinglePeriodCompareViewModel SingleVm { get; }
    public MultiStockCompareViewModel MultiVm { get; }

    public MainViewModel()
    {
        var svc = new EastMoneyService();
        SingleVm = new SinglePeriodCompareViewModel(svc);
        MultiVm = new MultiStockCompareViewModel(svc);
    }
}
