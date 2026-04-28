using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ComparetoolWpf.Models;
using ComparetoolWpf.Services;
using ComparetoolWpf.Views;
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
        ReportDateValue = new DateTime(year, 12, 31);
        RoeMinPercent = 10;
        RevenueYoYMinPercent = 10;
        NetProfitYoYMinPercent = 10;
    }

    /// <summary>报告期（DatePicker 绑定）。</summary>
    [ObservableProperty] private DateTime _reportDateValue;

    /// <summary>报告期格式化字符串。</summary>
    public string ReportDate => ReportDateValue.ToString("yyyy-MM-dd");

    partial void OnReportDateValueChanged(DateTime value) => OnPropertyChanged(nameof(ReportDate));

    /// <summary>单关键字（兼容旧用法，留作高级用途）。</summary>
    [ObservableProperty] private string? _industryContains;

    /// <summary>多选行业关键字。</summary>
    public ObservableCollection<string> SelectedIndustries { get; } = new();

    /// <summary>显示文本（行业按钮上）。</summary>
    public string IndustriesDisplay =>
        SelectedIndustries.Count == 0 ? "(全部行业)" :
        SelectedIndustries.Count <= 3 ? string.Join("/", SelectedIndustries) :
        $"{SelectedIndustries[0]}/{SelectedIndustries[1]} 等 {SelectedIndustries.Count} 项";

    [ObservableProperty] private double? _roeMinPercent;
    [ObservableProperty] private double? _grossMarginMinPercent;
    [ObservableProperty] private double? _netMarginMinPercent;
    [ObservableProperty] private double? _revenueYoYMinPercent;
    [ObservableProperty] private double? _netProfitYoYMinPercent;
    [ObservableProperty] private double? _epsMin;

    /// <summary>
    /// 最大翻页数。每页 200 只股票，按更新日期倒序拉取；
    /// MaxPages=5 意味着最多扫描 1000 只最近披露的股票。
    /// 全市场约 5000+ 只，需要扫描全市场可调到 30。
    /// </summary>
    [ObservableProperty] private int _maxPages = 5;

    public ObservableCollection<ScreenerRow> Results { get; } = new();
    [ObservableProperty] private string _statusText = string.Empty;

    [RelayCommand]
    private void PickIndustries()
    {
        var dlg = new IndustryPickerWindow(SelectedIndustries)
        {
            Owner = Application.Current.MainWindow,
        };
        if (dlg.ShowDialog() != true) return;
        SelectedIndustries.Clear();
        foreach (var n in dlg.SelectedIndustries) SelectedIndustries.Add(n);
        OnPropertyChanged(nameof(IndustriesDisplay));
    }

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
                IndustryAnyOf = SelectedIndustries.Count > 0 ? SelectedIndustries.ToList() : null,
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
            StatusText = $"共扫描 {MaxPages * 200} 只候选 → 命中 {Results.Count} 只";
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
