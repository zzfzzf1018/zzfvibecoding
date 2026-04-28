using System.IO;
using Newtonsoft.Json;

namespace ComparetoolWpf.Services;

/// <summary>数据源选择策略。</summary>
public enum DataSourceMode
{
    /// <summary>按 东方财富 → 雪球 → 新浪 顺序自动降级。</summary>
    Auto,
    EastMoney,
    Xueqiu,
    Sina,
}

/// <summary>
/// 应用设置。持久化到 <c>%LocalAppData%\ComparetoolWpf\settings.json</c>。
/// 修改 <see cref="Current"/> 后调用 <see cref="Save"/>，
/// 已实例化的 <see cref="StockDataService"/> 不会自动切换；通常在切换后提示用户重启或调用
/// <see cref="DataSourceFactory.RefreshFromSettings"/> 重建。
/// </summary>
public class AppSettings
{
    public DataSourceMode DataSource { get; set; } = DataSourceMode.Auto;

    /// <summary>每次接口调用最大重试次数（含首次）。</summary>
    public int RetryAttempts { get; set; } = 3;

    /// <summary>首次重试基础延时（毫秒），后续按 3× 指数退避。</summary>
    public int RetryBaseDelayMs { get; set; } = 500;

    /// <summary>HTTP 超时（秒）。</summary>
    public int HttpTimeoutSeconds { get; set; } = 20;

    public static string SettingsPath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "ComparetoolWpf", "settings.json");

    public static AppSettings Current { get; private set; } = Load();

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(SettingsPath))
            {
                var json = File.ReadAllText(SettingsPath);
                var s = JsonConvert.DeserializeObject<AppSettings>(json);
                if (s != null) return s;
            }
        }
        catch (Exception ex) { Logger.Warn($"加载设置失败：{ex.Message}"); }
        return new AppSettings();
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
            File.WriteAllText(SettingsPath, JsonConvert.SerializeObject(this, Formatting.Indented));
            Current = this;
            Logger.Info($"设置已保存：DataSource={DataSource}, Retry={RetryAttempts}, Timeout={HttpTimeoutSeconds}s");
        }
        catch (Exception ex) { Logger.Error("保存设置失败", ex); }
    }
}
