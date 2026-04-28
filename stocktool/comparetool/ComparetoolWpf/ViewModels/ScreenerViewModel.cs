using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ComparetoolWpf.Models;
using ComparetoolWpf.Services;
using Microsoft.Win32;

namespace ComparetoolWpf.ViewModels;

/// <summary>
/// 选股器 ViewModel：用户填写筛选条件，调用 <see cref="ScreenerService"/>。
/// </summary>
public partial class ScreenerViewModel : ObservableObject
{
    private readonly ScreenerService _screener;

    public ScreenerViewModel(ScreenerService screener)
    {
        _screener = screener;
        // 默认值：最近一次年报（保守地填上一年 12-31）
        var now = DateTime.Today;
        var year = now.Month >= 5 ? now.Year - 1 : now.Year - 2;
        ReportDate = $"{year}-12-31";
        RoeMinPercent = 10;
        RevenueYoYMinPercent = 10;
        NetProfitYoYMinPercent = 10;
    }

    /// <summary>报告期 (yyyy-MM-dd)。</summary>
    [ObservableProperty] private string _reportDate;

    [ObservableProperty] private string? _industryContains;
    [ObservableProperty] private double? _roeMinPercent;
    [ObservableProperty] private double? _grossMarginMinPercent;
    [ObservableProperty] private double? _netMarginMinPercent;
    [ObservableProperty] private double? _revenueYoYMinPercent;
    [ObservableProperty] private double? _netProfitYoYMinPercent;
    [ObservableProperty] private double? _epsMin;
    [ObservableProperty] private int _maxPages = 5;

    public ObservableCollection<ScreenerRow> Results { get; } = new();
    [ObservableProperty] private string _statusText = string.Empty;

    [RelayCommand]
    private async Task ScreenAsync()
    {
        StatusText = "查询中...";
        Results.Clear();
        try
        {
            var f = new ScreenerFilter
            {
                ReportDate = ReportDate,
                IndustryContains = string.IsNullOrWhiteSpace(IndustryContains) ? null : IndustryContains.Trim(),
                RoeMin = RoeMinPercent.HasValue ? RoeMinPercent / 100.0 : null,
                GrossMarginMin = GrossMarginMinPercent.HasValue ? GrossMarginMinPercent / 100.0 : null,
                NetMarginMin = NetMarginMinPercent.HasValue ? NetMarginMinPercent / 100.0 : null,
                RevenueYoYMin = RevenueYoYMinPercent.HasValue ? RevenueYoYMinPercent / 100.0 : null,
                NetProfitYoYMin = NetProfitYoYMinPercent.HasValue ? NetProfitYoYMinPercent / 100.0 : null,
                EpsMin = EpsMin,
                PageSize = 200,
            };
            var rows = await _screener.ScreenAsync(f, MaxPages);
            foreach (var r in rows) Results.Add(r);
            StatusText = $"共找到 {Results.Count} 只符合条件";
        }
        catch (Exception ex)
        {
            StatusText = "查询失败";
            MessageBox.Show($"选股失败：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    private void Export()
    {
        if (Results.Count == 0)
        {
            MessageBox.Show("没有可导出的数据。", "提示");
            return;
        }
        var dlg = new SaveFileDialog
        {
            Filter = "Excel 文件 (*.xlsx)|*.xlsx|CSV 文件 (*.csv)|*.csv",
            FileName = $"选股结果_{ReportDate}.xlsx",
        };
        if (dlg.ShowDialog() != true) return;
        if (dlg.FileName.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
            ExportService.ExportCsv(Results, dlg.FileName);
        else
            ExportService.ExportExcel(Results, dlg.FileName, "Screener");
    }
}
