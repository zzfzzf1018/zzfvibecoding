using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ComparetoolWpf.Models;
using ComparetoolWpf.Services;
using Microsoft.Win32;

namespace ComparetoolWpf.ViewModels;

/// <summary>
/// 单股票“期间对比”视图模型：
/// 用户搜索股票 -> 选择股票 -> 选择报表种类/报告期类型 -> 拉取多期 ->
/// 选择基准期与对比期 -> 计算差异（高亮超过阈值的指标）。
/// 数据访问通过 <see cref="StockDataService"/>，自动走 SQLite 缓存。
/// </summary>
public partial class SinglePeriodCompareViewModel : ObservableObject
{
    private readonly StockDataService _data;
    private readonly WatchlistService _watch;

    public SinglePeriodCompareViewModel(StockDataService data, WatchlistService watch)
    {
        _data = data;
        _watch = watch;
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

    // 输入防抖：每次输入推进 token，到时如未被替换则触发搜索
    private int _searchToken;

    partial void OnSearchKeywordChanged(string value)
    {
        // 如果当前文本恰好是 SelectedStock.ToString()，说明是用户从下拉中选中的，
        // 不再触发新的联想搜索，避免回环。
        if (SelectedStock != null && value == SelectedStock.ToString()) return;

        var token = System.Threading.Interlocked.Increment(ref _searchToken);
        AutoSearchAsync(value, token);
    }

    private async void AutoSearchAsync(string text, int token)
    {
        try
        {
            await Task.Delay(300); // 300ms 防抖
            if (token != _searchToken) return; // 期间又有输入，放弃本次
            if (string.IsNullOrWhiteSpace(text)) return;
            var list = await _data.SearchStocksAsync(text);
            if (token != _searchToken) return;
            SearchResults.Clear();
            foreach (var s in list) SearchResults.Add(s);
        }
        catch { /* 联想搜索静默失败 */ }
    }

    [RelayCommand]
    private async Task SearchAsync()
    {
        try
        {
            var list = await _data.SearchStocksAsync(SearchKeyword);
            SearchResults.Clear();
            foreach (var s in list) SearchResults.Add(s);
            if (SearchResults.Count > 0) SelectedStock = SearchResults[0];
        }
        catch (Exception ex)
        {
            MessageBox.Show($"搜索失败：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    /// <summary>全局自选股集合（供下拉选择、跨 Tab 共享）。</summary>
    public ObservableCollection<StockInfo> Watchlist => _watch.Items;

    [RelayCommand]
    private void ToggleWatch()
    {
        if (SelectedStock != null) _watch.Toggle(SelectedStock);
    }

    #endregion

    #region 报表加载

    public IReadOnlyList<ReportKind> ReportKinds { get; }
    public IReadOnlyList<ReportPeriodType> PeriodTypes { get; }
    [ObservableProperty] private ReportKind _selectedReportKind;
    [ObservableProperty] private ReportPeriodType _selectedPeriodType;
    /// <summary>是否强制忽略缓存，重新拉取。</summary>
    [ObservableProperty] private bool _forceRefresh;

    public ObservableCollection<FinancialReport> LoadedReports { get; } = new();
    [ObservableProperty] private FinancialReport? _baseReport;
    [ObservableProperty] private FinancialReport? _compareReport;

    /// <summary>展示数据来源时间（如 "缓存 2026-04-15 12:34"）。</summary>
    [ObservableProperty] private string _dataSourceText = string.Empty;

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
            var reports = await _data.GetReportsAsync(
                SelectedStock, SelectedReportKind, SelectedPeriodType,
                pageSize: 20, forceRefresh: ForceRefresh);

            LoadedReports.Clear();
            foreach (var r in reports) LoadedReports.Add(r);

            if (LoadedReports.Count >= 1) CompareReport = LoadedReports[0];
            if (LoadedReports.Count >= 2) BaseReport = LoadedReports[1];

            var fetched = _data.GetLastFetchedAtLocal(SelectedStock.FullCode, SelectedReportKind);
            DataSourceText = fetched.HasValue
                ? $"数据时间: {fetched:yyyy-MM-dd HH:mm} ({(DateTime.Now - fetched.Value).TotalDays:F1} 天前)"
                : string.Empty;

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

    #region 导出

    [RelayCommand]
    private void ExportComparison()
    {
        if (ComparisonRows.Count == 0)
        {
            MessageBox.Show("没有可导出的对比数据，请先执行对比。", "提示");
            return;
        }
        var dlg = new SaveFileDialog
        {
            Filter = "Excel 文件 (*.xlsx)|*.xlsx|CSV 文件 (*.csv)|*.csv",
            FileName = $"{SelectedStock?.Name}_{SelectedReportKind}_对比.xlsx",
        };
        if (dlg.ShowDialog() != true) return;
        try
        {
            if (dlg.FileName.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
                ExportService.ExportCsv(ComparisonRows, dlg.FileName);
            else
                ExportService.ExportExcel(ComparisonRows, dlg.FileName, "Comparison");
            MessageBox.Show($"导出成功：{dlg.FileName}", "提示");
        }
        catch (Exception ex)
        {
            MessageBox.Show($"导出失败：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    #endregion
}
