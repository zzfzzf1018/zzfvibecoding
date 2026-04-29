using System.Globalization;
using System.Net.Http;
using System.Text.Json;
using StockTechAnalyzer.Models;

namespace StockTechAnalyzer.Services;

/// <summary>
/// 当日 1 分钟分时数据（腾讯接口，免费、无 token）。
/// </summary>
public sealed class MinuteDataSource
{
    private readonly HttpClient _http;
    public MinuteDataSource()
    {
        _http = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 StockTechAnalyzer/1.0");
    }

    public sealed record Tick(DateTime Time, double Price, double AvgPrice, double Volume);

    public async Task<IReadOnlyList<Tick>> GetTodayAsync(StockInfo stock, CancellationToken ct = default)
    {
        // tencent: web.ifzq.gtimg.cn/appstock/app/minute/query?code=sh600000
        var url = $"https://web.ifzq.gtimg.cn/appstock/app/minute/query?code={stock.FullCode}";
        var json = await _http.GetStringAsync(url, ct).ConfigureAwait(false);
        using var doc = JsonDocument.Parse(json);
        if (!doc.RootElement.TryGetProperty("data", out var data)) return Array.Empty<Tick>();
        if (!data.TryGetProperty(stock.FullCode, out var stockNode)) return Array.Empty<Tick>();
        if (!stockNode.TryGetProperty("data", out var inner)) return Array.Empty<Tick>();
        if (!inner.TryGetProperty("data", out var arr) || arr.ValueKind != JsonValueKind.Array)
            return Array.Empty<Tick>();
        string? dateStr = inner.TryGetProperty("date", out var dEl) ? dEl.GetString() : DateTime.Today.ToString("yyyyMMdd");
        DateTime baseDate = DateTime.TryParseExact(dateStr, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var bd)
            ? bd : DateTime.Today;

        var ci = CultureInfo.InvariantCulture;
        var list = new List<Tick>();
        foreach (var el in arr.EnumerateArray())
        {
            var s = el.GetString();
            if (string.IsNullOrEmpty(s)) continue;
            // "0930 11.50 11.48 1234" => time price avg vol
            var p = s.Split(' ');
            if (p.Length < 4) continue;
            if (p[0].Length != 4) continue;
            int hh = int.Parse(p[0].Substring(0, 2));
            int mm = int.Parse(p[0].Substring(2, 2));
            list.Add(new Tick(
                baseDate.AddHours(hh).AddMinutes(mm),
                double.Parse(p[1], ci),
                double.Parse(p[2], ci),
                double.Parse(p[3], ci)
            ));
        }
        return list;
    }
}
