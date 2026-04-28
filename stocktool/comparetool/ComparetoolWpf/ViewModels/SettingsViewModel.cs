using System.Diagnostics;
using System.IO;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ComparetoolWpf.Models;
using ComparetoolWpf.Services;

namespace ComparetoolWpf.ViewModels;

/// <summary>
/// 设置页 ViewModel：切换数据源、调整重试参数等。
/// </summary>
public partial class SettingsViewModel : ObservableObject
{
    public SettingsViewModel()
    {
        var s = AppSettings.Current;
        DataSource = s.DataSource;
        RetryAttempts = s.RetryAttempts;
        RetryBaseDelayMs = s.RetryBaseDelayMs;
        HttpTimeoutSeconds = s.HttpTimeoutSeconds;
        UpdateCurrentSourceText();
    }

    public IReadOnlyList<DataSourceMode> AllModes { get; } = new[]
    {
        DataSourceMode.Auto,
        DataSourceMode.EastMoney,
        DataSourceMode.Xueqiu,
        DataSourceMode.Sina,
    };

    [ObservableProperty] private DataSourceMode _dataSource;
    [ObservableProperty] private int _retryAttempts;
    [ObservableProperty] private int _retryBaseDelayMs;
    [ObservableProperty] private int _httpTimeoutSeconds;

    [ObservableProperty] private string _currentSourceText = string.Empty;
    [ObservableProperty] private string _testResult = string.Empty;

    public string SettingsPath => AppSettings.SettingsPath;
    public string LogDirectory => Logger.LogDirectory;

    private void UpdateCurrentSourceText()
        => CurrentSourceText = $"当前生效：{DataSourceFactory.Current.Name}";

    public string DataSourceDescription => DataSource switch
    {
        DataSourceMode.Auto => "按 东方财富 → 雪球 → 新浪 顺序自动降级，单条失败就降级到下一个。推荐。",
        DataSourceMode.EastMoney => "只用东方财富。字段最全；偶尔被风控时建议切到自动模式。",
        DataSourceMode.Xueqiu => "只用雪球。三大报表覆盖完整；首次访问会自动取 cookie。",
        DataSourceMode.Sina => "只用新浪。仅搜索可用；三大报表不支持(无 JSON 接口)，加载报表会失败。",
        _ => string.Empty,
    };

    partial void OnDataSourceChanged(DataSourceMode value) => OnPropertyChanged(nameof(DataSourceDescription));

    [RelayCommand]
    private void Save()
    {
        var s = new AppSettings
        {
            DataSource = DataSource,
            RetryAttempts = Math.Max(1, RetryAttempts),
            RetryBaseDelayMs = Math.Max(0, RetryBaseDelayMs),
            HttpTimeoutSeconds = Math.Max(1, HttpTimeoutSeconds),
        };
        s.Save();
        DataSourceFactory.RefreshFromSettings();
        UpdateCurrentSourceText();
        MessageBox.Show($"设置已保存并立即生效。\n当前数据源：{DataSourceFactory.Current.Name}",
            "提示", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    [RelayCommand]
    private void Reset()
    {
        var d = new AppSettings();
        DataSource = d.DataSource;
        RetryAttempts = d.RetryAttempts;
        RetryBaseDelayMs = d.RetryBaseDelayMs;
        HttpTimeoutSeconds = d.HttpTimeoutSeconds;
    }

    [RelayCommand]
    private async Task TestSourceAsync()
    {
        TestResult = "测试中...";
        try
        {
            // 先把当前 UI 选项当作临时设置应用
            var snapshot = AppSettings.Current;
            var temp = new AppSettings
            {
                DataSource = DataSource,
                RetryAttempts = RetryAttempts,
                RetryBaseDelayMs = RetryBaseDelayMs,
                HttpTimeoutSeconds = HttpTimeoutSeconds,
            };
            temp.Save();
            DataSourceFactory.RefreshFromSettings();

            var src = DataSourceFactory.Current;
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var list = await src.SearchStocksAsync("浦发");
            sw.Stop();
            TestResult = $"✓ {src.Name}: 搜索 \"浦发\" 命中 {list.Count} 条，耗时 {sw.ElapsedMilliseconds} ms";
            UpdateCurrentSourceText();
        }
        catch (Exception ex)
        {
            TestResult = $"✗ 失败：{ex.Message}";
        }
    }

    [RelayCommand]
    private void OpenLogFolder()
    {
        try { Process.Start(new ProcessStartInfo("explorer.exe", LogDirectory) { UseShellExecute = true }); }
        catch (Exception ex) { MessageBox.Show($"打开失败：{ex.Message}"); }
    }

    [RelayCommand]
    private void OpenSettingsFile()
    {
        try
        {
            var dir = Path.GetDirectoryName(SettingsPath);
            if (dir != null) Process.Start(new ProcessStartInfo("explorer.exe", dir) { UseShellExecute = true });
        }
        catch (Exception ex) { MessageBox.Show($"打开失败：{ex.Message}"); }
    }
}
