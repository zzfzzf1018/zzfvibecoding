using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ComparetoolWpf.Models;
using ComparetoolWpf.Services;
using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Series;

namespace ComparetoolWpf.ViewModels;

/// <summary>
/// 趋势图视图模型：可同时绘制多只股票同一指标的多期趋势曲线（OxyPlot）。
/// </summary>
public partial class TrendChartViewModel : ObservableObject
{
    private readonly StockDataService _data;

    public TrendChartViewModel(StockDataService data)
    {
        _data = data;
        ReportKinds = new[] { ReportKind.Balance, ReportKind.Income, ReportKind.CashFlow };
        PeriodTypes = new[]
        {
            ReportPeriodType.Annual, ReportPeriodType.SemiAnnual,
            ReportPeriodType.Q1, ReportPeriodType.Q3, ReportPeriodType.All,
        };
        // 注意：AllMetrics 必须在 SelectedReportKind 之前初始化，
        // 因为对 SelectedReportKind 赋值会触发 OnSelectedReportKindChanged，
        // 而该回调会读写 AllMetrics。
        AllMetrics = new ObservableCollection<string>(EastMoneyService.IncomeFieldMap.Values);
        SelectedReportKind = ReportKind.Income;
        SelectedPeriodType = ReportPeriodType.Annual;
        SelectedMetric = "营业总收入";
        PlotModel = BuildEmptyModel();
    }

    [ObservableProperty] private string _searchKeyword = string.Empty;
    public ObservableCollection<StockInfo> SearchResults { get; } = new();
    [ObservableProperty] private StockInfo? _searchSelected;

    public ObservableCollection<StockInfo> SelectedStocks { get; } = new();
    [ObservableProperty] private StockInfo? _selectedStocksItem;

    public IReadOnlyList<ReportKind> ReportKinds { get; }
    public IReadOnlyList<ReportPeriodType> PeriodTypes { get; }
    [ObservableProperty] private ReportKind _selectedReportKind;
    [ObservableProperty] private ReportPeriodType _selectedPeriodType;

    public ObservableCollection<string> AllMetrics { get; }
    [ObservableProperty] private string? _selectedMetric;

    [ObservableProperty] private PlotModel _plotModel;

    partial void OnSelectedReportKindChanged(ReportKind value)
    {
        // 防御性：构造函数赋值时 AllMetrics 可能尚未实例化
        if (AllMetrics == null) return;

        var dict = value switch
        {
            ReportKind.Balance => EastMoneyService.BalanceFieldMap,
            ReportKind.Income => EastMoneyService.IncomeFieldMap,
            ReportKind.CashFlow => EastMoneyService.CashFlowFieldMap,
            _ => new Dictionary<string, string>(),
        };
        AllMetrics.Clear();
        foreach (var v in dict.Values) AllMetrics.Add(v);
        if (!AllMetrics.Contains(SelectedMetric ?? "")) SelectedMetric = AllMetrics.FirstOrDefault();
    }

    [RelayCommand]
    private async Task SearchAsync()
    {
        try
        {
            var list = await _data.SearchStocksAsync(SearchKeyword);
            SearchResults.Clear();
            foreach (var s in list) SearchResults.Add(s);
            if (SearchResults.Count > 0) SearchSelected = SearchResults[0];
        }
        catch (Exception ex) { MessageBox.Show($"搜索失败：{ex.Message}", "错误"); }
    }

    [RelayCommand]
    private void AddStock()
    {
        if (SearchSelected is null) return;
        if (SelectedStocks.Any(s => s.FullCode == SearchSelected.FullCode)) return;
        SelectedStocks.Add(SearchSelected);
    }

    [RelayCommand]
    private void RemoveStock()
    {
        if (SelectedStocksItem is null) return;
        SelectedStocks.Remove(SelectedStocksItem);
    }

    [RelayCommand]
    private async Task PlotAsync()
    {
        if (SelectedStocks.Count == 0 || string.IsNullOrEmpty(SelectedMetric))
        {
            MessageBox.Show("请添加至少一只股票并选择一个指标。", "提示");
            return;
        }

        var model = new PlotModel
        {
            Title = $"{SelectedMetric} 趋势 ({SelectedReportKind} / {SelectedPeriodType})",
            IsLegendVisible = true,
        };
        model.Legends.Add(new OxyPlot.Legends.Legend
        {
            LegendPlacement = OxyPlot.Legends.LegendPlacement.Outside,
            LegendPosition = OxyPlot.Legends.LegendPosition.RightTop,
        });
        model.Axes.Add(new DateTimeAxis
        {
            Position = AxisPosition.Bottom,
            StringFormat = "yyyy-MM",
            Title = "报告期",
            IntervalType = DateTimeIntervalType.Years,
        });
        model.Axes.Add(new LinearAxis
        {
            Position = AxisPosition.Left,
            Title = SelectedMetric,
        });

        try
        {
            foreach (var stock in SelectedStocks)
            {
                var reports = await _data.GetReportsAsync(stock, SelectedReportKind, SelectedPeriodType, pageSize: 30);
                var series = new LineSeries
                {
                    Title = stock.Name,
                    MarkerType = MarkerType.Circle,
                    MarkerSize = 4,
                    StrokeThickness = 2,
                };
                foreach (var r in reports.OrderBy(r => r.ReportDate))
                {
                    if (r.Items.TryGetValue(SelectedMetric, out var v) && v.HasValue)
                    {
                        series.Points.Add(DateTimeAxis.CreateDataPoint(r.ReportDate, v.Value));
                    }
                }
                if (series.Points.Count > 0) model.Series.Add(series);
            }
            PlotModel = model;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"绘图失败：{ex.Message}", "错误");
        }
    }

    private static PlotModel BuildEmptyModel()
    {
        var m = new PlotModel { Title = "选择指标后点击 [绘制趋势]" };
        return m;
    }
}
