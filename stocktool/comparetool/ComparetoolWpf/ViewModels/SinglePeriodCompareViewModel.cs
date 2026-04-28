using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ComparetoolWpf.Models;
using ComparetoolWpf.Services;

namespace ComparetoolWpf.ViewModels;

/// <summary>
/// 单股票“期间对比”视图模型：
/// 用户搜索股票 -> 选择股票 -> 选择报表种类/报告期类型 -> 拉取多期 ->
/// 选择基准期与对比期 -> 计算差异（高亮超过阈值的指标）。
/// </summary>
public partial class SinglePeriodCompareViewModel : ObservableObject
{
    private readonly EastMoneyService _service;

    public SinglePeriodCompareViewModel(EastMoneyService service)
    {
        _service = service;
        ReportKinds = new[] { ReportKind.Balance, ReportKind.Income, ReportKind.CashFlow };
        PeriodTypes = new[]
        {
            ReportPeriodType.All, ReportPeriodType.Annual, ReportPeriodType.SemiAnnual,
            ReportPeriodType.Q1, ReportPeriodType.Q3,
        };
        SelectedReportKind = ReportKind.Balance;
        SelectedPeriodType = ReportPeriodType.Annual;
        HighlightThresholdPercent = 20; // 默认 ±20%
    }

    #region 搜索

    [ObservableProperty] private string _searchKeyword = string.Empty;
    public ObservableCollection<StockInfo> SearchResults { get; } = new();
    [ObservableProperty] private StockInfo? _selectedStock;

    [RelayCommand]
    private async Task SearchAsync()
    {
        try
        {
            var list = await _service.SearchStocksAsync(SearchKeyword);
            SearchResults.Clear();
            foreach (var s in list) SearchResults.Add(s);
            if (SearchResults.Count > 0) SelectedStock = SearchResults[0];
        }
        catch (Exception ex)
        {
            MessageBox.Show($"搜索失败：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    #endregion

    #region 报表加载

    public IReadOnlyList<ReportKind> ReportKinds { get; }
    public IReadOnlyList<ReportPeriodType> PeriodTypes { get; }
    [ObservableProperty] private ReportKind _selectedReportKind;
    [ObservableProperty] private ReportPeriodType _selectedPeriodType;

    public ObservableCollection<FinancialReport> LoadedReports { get; } = new();
    [ObservableProperty] private FinancialReport? _baseReport;
    [ObservableProperty] private FinancialReport? _compareReport;

    [RelayCommand]
    private async Task LoadReportsAsync()
    {
        if (SelectedStock is null)
        {
            MessageBox.Show("请先搜索并选择一只股票。", "提示");
            return;
        }
        try
        {
            var reports = await _service.GetReportsAsync(SelectedStock, SelectedReportKind, SelectedPeriodType);
            LoadedReports.Clear();
            foreach (var r in reports) LoadedReports.Add(r);

            // 默认对比最新两期：CompareReport=最新, BaseReport=次新
            if (LoadedReports.Count >= 1) CompareReport = LoadedReports[0];
            if (LoadedReports.Count >= 2) BaseReport = LoadedReports[1];

            ComparisonRows.Clear();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"加载报表失败：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    #endregion

    #region 对比

    /// <summary>变化高亮阈值（百分数，例如 20 = 20%）。</summary>
    [ObservableProperty] private double _highlightThresholdPercent;

    public ObservableCollection<ComparisonRow> ComparisonRows { get; } = new();

    [RelayCommand]
    private void Compare()
    {
        if (BaseReport is null || CompareReport is null)
        {
            MessageBox.Show("请选择基准期与对比期。", "提示");
            return;
        }
        var rows = ComparisonService.ComparePeriods(BaseReport, CompareReport, HighlightThresholdPercent / 100.0);
        ComparisonRows.Clear();
        foreach (var r in rows) ComparisonRows.Add(r);
    }

    #endregion
}
