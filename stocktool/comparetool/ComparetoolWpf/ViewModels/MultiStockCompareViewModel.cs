using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ComparetoolWpf.Models;
using ComparetoolWpf.Services;

namespace ComparetoolWpf.ViewModels;

/// <summary>
/// 多股票横向对比视图模型。
/// 用户搜索 -> 加入对比池 -> 选择报表种类 + 报告期类型 ->
/// 加载每只股票最新一期同类型报表 -> 计算同口径百分比 -> 展示。
/// </summary>
public partial class MultiStockCompareViewModel : ObservableObject
{
    private readonly EastMoneyService _service;

    public MultiStockCompareViewModel(EastMoneyService service)
    {
        _service = service;
        ReportKinds = new[] { ReportKind.Balance, ReportKind.Income, ReportKind.CashFlow };
        PeriodTypes = new[]
        {
            ReportPeriodType.Annual, ReportPeriodType.SemiAnnual,
            ReportPeriodType.Q1, ReportPeriodType.Q3,
        };
        SelectedReportKind = ReportKind.Balance;
        SelectedPeriodType = ReportPeriodType.Annual;

        SelectedStocks.CollectionChanged += (_, _) => RebuildView();
    }

    #region 搜索 / 选择股票池

    [ObservableProperty] private string _searchKeyword = string.Empty;
    public ObservableCollection<StockInfo> SearchResults { get; } = new();
    [ObservableProperty] private StockInfo? _searchSelectedStock;

    /// <summary>已加入对比的股票池。</summary>
    public ObservableCollection<StockInfo> SelectedStocks { get; } = new();
    [ObservableProperty] private StockInfo? _selectedStocksItem;

    [RelayCommand]
    private async Task SearchAsync()
    {
        try
        {
            var list = await _service.SearchStocksAsync(SearchKeyword);
            SearchResults.Clear();
            foreach (var s in list) SearchResults.Add(s);
            if (SearchResults.Count > 0) SearchSelectedStock = SearchResults[0];
        }
        catch (Exception ex)
        {
            MessageBox.Show($"搜索失败：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    private void AddStock()
    {
        if (SearchSelectedStock is null) return;
        if (SelectedStocks.Any(s => s.FullCode == SearchSelectedStock.FullCode)) return;
        SelectedStocks.Add(SearchSelectedStock);
    }

    [RelayCommand]
    private void RemoveStock()
    {
        if (SelectedStocksItem is null) return;
        SelectedStocks.Remove(SelectedStocksItem);
    }

    #endregion

    #region 报表加载与对比

    public IReadOnlyList<ReportKind> ReportKinds { get; }
    public IReadOnlyList<ReportPeriodType> PeriodTypes { get; }
    [ObservableProperty] private ReportKind _selectedReportKind;
    [ObservableProperty] private ReportPeriodType _selectedPeriodType;

    /// <summary>同口径行集合。</summary>
    public ObservableCollection<MultiStockRow> Rows { get; } = new();

    /// <summary>
    /// 列定义：每只股票一列。供 View 通过代码动态生成 DataGrid 列。
    /// </summary>
    public ObservableCollection<StockInfo> ColumnsStocks { get; } = new();

    public event EventHandler? ColumnsRebuildRequested;

    [RelayCommand]
    private async Task LoadAndCompareAsync()
    {
        if (SelectedStocks.Count == 0)
        {
            MessageBox.Show("请至少添加一只股票到对比池。", "提示");
            return;
        }
        try
        {
            var reports = new List<FinancialReport>();
            foreach (var s in SelectedStocks)
            {
                var list = await _service.GetReportsAsync(s, SelectedReportKind, SelectedPeriodType, pageSize: 1);
                if (list.Count > 0) reports.Add(list[0]);
            }
            if (reports.Count == 0)
            {
                MessageBox.Show("没有获取到任何报表。", "提示");
                return;
            }

            var rows = ComparisonService.CommonSizeCompare(reports, SelectedReportKind);
            Rows.Clear();
            foreach (var r in rows) Rows.Add(r);

            ColumnsStocks.Clear();
            foreach (var s in SelectedStocks) ColumnsStocks.Add(s);
            ColumnsRebuildRequested?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"加载/对比失败：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void RebuildView()
    {
        // 选股池变动时只更新列模型；具体重绘由 View 侧响应事件
    }

    #endregion
}
