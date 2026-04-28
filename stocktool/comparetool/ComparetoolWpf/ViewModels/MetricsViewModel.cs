using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ComparetoolWpf.Models;
using ComparetoolWpf.Services;
using Microsoft.Win32;

namespace ComparetoolWpf.ViewModels;

/// <summary>
/// 指标计算视图模型：
///   - 选择股票后，分别加载 利润表/资产负债表/现金流量表（默认年报）；
///   - 选择一个指标 -> 计算同比 / 环比 序列；
///   - 计算 ROE 杜邦拆解（需要利润表 + 资产负债表）。
/// </summary>
public partial class MetricsViewModel : ObservableObject
{
    private readonly StockDataService _data;

    public MetricsViewModel(StockDataService data)
    {
        _data = data;
        PeriodTypes = new[]
        {
            ReportPeriodType.Annual, ReportPeriodType.SemiAnnual,
            ReportPeriodType.Q1, ReportPeriodType.Q3, ReportPeriodType.All,
        };
        SelectedPeriodType = ReportPeriodType.Annual;
    }

    [ObservableProperty] private string _searchKeyword = string.Empty;
    public ObservableCollection<StockInfo> SearchResults { get; } = new();
    [ObservableProperty] private StockInfo? _selectedStock;

    public IReadOnlyList<ReportPeriodType> PeriodTypes { get; }
    [ObservableProperty] private ReportPeriodType _selectedPeriodType;

    /// <summary>所有可选指标（来自三大报表的字段映射的并集）。</summary>
    public ObservableCollection<string> AllMetrics { get; } = new();
    [ObservableProperty] private string? _selectedMetric;

    public ObservableCollection<GrowthRow> GrowthRows { get; } = new();
    public ObservableCollection<DuPontRow> DuPontRows { get; } = new();

    private List<FinancialReport> _balances = new();
    private List<FinancialReport> _incomes = new();
    private List<FinancialReport> _cashFlows = new();

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
            MessageBox.Show($"搜索失败：{ex.Message}", "错误");
        }
    }

    /// <summary>加载三大报表 + 计算 ROE 杜邦。</summary>
    [RelayCommand]
    private async Task LoadAndAnalyzeAsync()
    {
        if (SelectedStock is null)
        {
            MessageBox.Show("请先搜索并选择一只股票。", "提示");
            return;
        }
        try
        {
            _balances = await _data.GetReportsAsync(SelectedStock, ReportKind.Balance, SelectedPeriodType, pageSize: 20);
            _incomes = await _data.GetReportsAsync(SelectedStock, ReportKind.Income, SelectedPeriodType, pageSize: 20);
            _cashFlows = await _data.GetReportsAsync(SelectedStock, ReportKind.CashFlow, SelectedPeriodType, pageSize: 20);

            // 汇总指标列表
            var metrics = new HashSet<string>();
            foreach (var r in _balances.Concat(_incomes).Concat(_cashFlows))
                foreach (var k in r.Items.Keys) metrics.Add(k);

            AllMetrics.Clear();
            foreach (var m in metrics.OrderBy(s => s)) AllMetrics.Add(m);
            SelectedMetric ??= AllMetrics.FirstOrDefault(m => m == "归属母公司股东净利润") ?? AllMetrics.FirstOrDefault();

            // 计算杜邦
            DuPontRows.Clear();
            foreach (var d in MetricsService.ComputeDuPont(_balances, _incomes))
                DuPontRows.Add(d);

            ComputeGrowthInternal();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"加载失败：{ex.Message}", "错误");
        }
    }

    partial void OnSelectedMetricChanged(string? value) => ComputeGrowthInternal();

    private void ComputeGrowthInternal()
    {
        GrowthRows.Clear();
        if (string.IsNullOrEmpty(SelectedMetric)) return;

        // 在三个报表中找包含该指标的那一份
        var src = new[] { _incomes, _balances, _cashFlows }
            .FirstOrDefault(list => list.Any(r => r.Items.ContainsKey(SelectedMetric)));
        if (src == null) return;

        foreach (var g in MetricsService.ComputeGrowth(src, SelectedMetric))
            GrowthRows.Add(g);
    }

    [RelayCommand]
    private void ExportDuPont()
    {
        if (DuPontRows.Count == 0)
        {
            MessageBox.Show("没有可导出的杜邦数据。", "提示");
            return;
        }
        var dlg = new SaveFileDialog
        {
            Filter = "Excel 文件 (*.xlsx)|*.xlsx|CSV 文件 (*.csv)|*.csv",
            FileName = $"{SelectedStock?.Name}_杜邦分析.xlsx",
        };
        if (dlg.ShowDialog() != true) return;
        if (dlg.FileName.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
            ExportService.ExportCsv(DuPontRows, dlg.FileName);
        else
            ExportService.ExportExcel(DuPontRows, dlg.FileName, "DuPont");
    }

    [RelayCommand]
    private void ExportGrowth()
    {
        if (GrowthRows.Count == 0)
        {
            MessageBox.Show("没有可导出的同比/环比数据。", "提示");
            return;
        }
        var dlg = new SaveFileDialog
        {
            Filter = "Excel 文件 (*.xlsx)|*.xlsx|CSV 文件 (*.csv)|*.csv",
            FileName = $"{SelectedStock?.Name}_{SelectedMetric}_同比环比.xlsx",
        };
        if (dlg.ShowDialog() != true) return;
        if (dlg.FileName.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
            ExportService.ExportCsv(GrowthRows, dlg.FileName);
        else
            ExportService.ExportExcel(GrowthRows, dlg.FileName, "Growth");
    }
}
