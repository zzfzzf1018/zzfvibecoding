using System.Text.Json;
using StockTechAnalyzer.Models;

namespace StockTechAnalyzer.Storage;

/// <summary>
/// 自选股 + 应用配置（数据源/Tushare token）持久化到本地 JSON。
/// </summary>
public sealed class AppSettings
{
    public string DataSource { get; set; } = "Sina"; // Sina / EastMoney / Tushare
    public string TushareToken { get; set; } = "";
    public bool DarkMode { get; set; } = false;
    public bool EnableCache { get; set; } = true;
    public List<StockInfo> Watchlist { get; set; } = new();

    private static string FilePath
    {
        get
        {
            var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "StockTechAnalyzer");
            Directory.CreateDirectory(dir);
            return Path.Combine(dir, "settings.json");
        }
    }

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                var json = File.ReadAllText(FilePath);
                return JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
            }
        }
        catch { /* 损坏则重建 */ }
        return new AppSettings();
    }

    public void Save()
    {
        var json = JsonSerializer.Serialize(this, new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        });
        File.WriteAllText(FilePath, json);
    }
}
