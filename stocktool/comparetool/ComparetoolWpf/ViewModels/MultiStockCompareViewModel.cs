using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ComparetoolWpf.Models;
using ComparetoolWpf.Services;
using Microsoft.Win32;

namespace ComparetoolWpf.ViewModels;

/// <summary>
/// 多股票横向对比视图模型。
/// 用户搜索 -> 加入对比池 -> 选择报表种类 + 报告期类型 ->
/// 加载每只股票最新一期同类型报表 -> 计算同口径百分比 -> 展示。
/// 数据访问通过 <see cref="StockDataService"/>，自动走 SQLite 缓存。
/// </summary>
public partial class MultiStockCompareViewModel : ObservableObject
{
    private readonly StockDataService _data;

    public MultiStockCompareViewModel(StockDataService data)
    {
        _data = data;
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

    private int _searchToken;
    partial void OnSearchKeywordChanged(string value)
    {
        if (SearchSelectedStock != null && value == SearchSelectedStock.ToString()) return;
        var token = System.Threading.Interlocked.Increment(ref _searchToken);
        _ = AutoSearchAsync(value, token);
    }
    private async Task AutoSearchAsync(string text, int token)
    {
        try
        {
            await Task.Delay(300);
            if (token != _searchToken || string.IsNullOrWhiteSpace(text)) return;
            var list = await _data.SearchStocksAsync(text);
            if (token != _searchToken) return;
            SearchResults.Clear();
            foreach (var s in list) SearchResults.Add(s);
        }
        catch { }
    }

    /// <summary>已加入对比的股票池。</summary>
    public ObservableCollection<StockInfo> SelectedStocks { get; } = new();
    [ObservableProperty] private StockInfo? _selectedStocksItem;

    [RelayCommand]
    private async Task SearchAsync()
    {
        try
        {
            var list = await _data.SearchStocksAsync(SearchKeyword);
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
                var list = await _data.GetReportsAsync(s, SelectedReportKind, SelectedPeriodType, pageSize: 1);
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

    #region 导出

    /// <summary>把多股横向对比的结果（指标 + 各股原值 + 各股占比）扁平化导出。</summary>
    [RelayCommand]
    private void ExportRows()
    {
        if (Rows.Count == 0)
        {
            MessageBox.Show("没有可导出的数据，请先执行对比。", "提示");
            return;
        }
        var dlg = new SaveFileDialog
        {
            Filter = "Excel 文件 (*.xlsx)|*.xlsx|CSV 文件 (*.csv)|*.csv",
            FileName = $"多股对比_{SelectedReportKind}.xlsx",
        };
        if (dlg.ShowDialog() != true) return;
        try
        {
            // 转换为扁平结构：指标 + 每只股票两列
            var flat = Rows.Select(r =>
            {
                var dict = new Dictionary<string, object?> { ["指标"] = r.Item };
                foreach (var s in ColumnsStocks)
                {
                    dict[$"{s.Name}_数值"] = r.RawValues.GetValueOrDefault(s.FullCode);
                    dict[$"{s.Name}_占比"] = r.Percentages.GetValueOrDefault(s.FullCode);
                }
                return dict;
            }).ToList();

            if (dlg.FileName.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
                ExportDictListAsCsv(flat, dlg.FileName);
            else
                ExportDictListAsExcel(flat, dlg.FileName);
            MessageBox.Show($"导出成功：{dlg.FileName}", "提示");
        }
        catch (Exception ex)
        {
            MessageBox.Show($"导出失败：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private static void ExportDictListAsCsv(List<Dictionary<string, object?>> rows, string path)
    {
        if (rows.Count == 0) return;
        var headers = rows[0].Keys.ToList();
        var sb = new System.Text.StringBuilder();
        sb.AppendLine(string.Join(",", headers));
        foreach (var r in rows)
        {
            sb.AppendLine(string.Join(",", headers.Select(h =>
            {
                var v = r.GetValueOrDefault(h);
                return v switch
                {
                    null => string.Empty,
                    double d => d.ToString("R", System.Globalization.CultureInfo.InvariantCulture),
                    _ => v.ToString() ?? string.Empty,
                };
            })));
        }
        System.IO.File.WriteAllText(path, sb.ToString(), new System.Text.UTF8Encoding(true));
    }

    private static void ExportDictListAsExcel(List<Dictionary<string, object?>> rows, string path)
    {
        using var wb = new ClosedXML.Excel.XLWorkbook();
        var ws = wb.AddWorksheet("MultiStock");
        if (rows.Count == 0)
        {
            wb.SaveAs(path);
            return;
        }
        var headers = rows[0].Keys.ToList();
        for (int c = 0; c < headers.Count; c++) ws.Cell(1, c + 1).Value = headers[c];
        for (int r = 0; r < rows.Count; r++)
        {
            for (int c = 0; c < headers.Count; c++)
            {
                var v = rows[r].GetValueOrDefault(headers[c]);
                ws.Cell(r + 2, c + 1).Value = v switch
                {
                    null => ClosedXML.Excel.Blank.Value,
                    double d => d,
                    string s => s,
                    _ => v.ToString() ?? string.Empty,
                };
            }
        }
        ws.Columns().AdjustToContents();
        wb.SaveAs(path);
    }

    #endregion
}
