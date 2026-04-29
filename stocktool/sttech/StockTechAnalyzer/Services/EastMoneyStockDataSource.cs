using System.Globalization;
using System.Net.Http;
using System.Text.Json;
using StockTechAnalyzer.Models;

namespace StockTechAnalyzer.Services;

/// <summary>
/// 东方财富数据源（A 股，免费、无需 token）。
/// K 线接口：push2his.eastmoney.com/api/qt/stock/kline/get
/// 实时报价：push2.eastmoney.com/api/qt/stock/get
/// 搜索：复用新浪（东财 suggest 接口需 token，复用更稳定）。
/// </summary>
public sealed class EastMoneyStockDataSource : IStockDataSource
{
    public string Name => "东方财富";

    private readonly HttpClient _http;
    private readonly SinaStockDataSource _sinaForSearch;

    public EastMoneyStockDataSource(SinaStockDataSource sinaForSearch)
    {
        _sinaForSearch = sinaForSearch;
        _http = new HttpClient(new HttpClientHandler
        {
            AutomaticDecompression = System.Net.DecompressionMethods.All,
        })
        {
            Timeout = TimeSpan.FromSeconds(15),
        };
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 StockTechAnalyzer/1.0");
        _http.DefaultRequestHeaders.Referrer = new Uri("https://quote.eastmoney.com/");
    }

    public Task<IReadOnlyList<StockInfo>> SearchAsync(string keyword, CancellationToken ct = default)
        => _sinaForSearch.SearchAsync(keyword, ct);

    // ---- secid 拼接 ---------------------------------------------------
    // 沪市(6/9): 1.xxxxxx ；深市(0/3): 0.xxxxxx ；北交所(4/8): 0.xxxxxx
    private static string BuildSecId(StockInfo s)
    {
        var c = s.Code;
        if (c.Length == 0) return "1." + c;
        if (s.Market == "sh") return "1." + c;
        return "0." + c; // sz / bj
    }

    // ---- 实时报价 ------------------------------------------------------
    public async Task<RealtimeQuote?> GetQuoteAsync(StockInfo stock, CancellationToken ct = default)
    {
        var url = $"https://push2.eastmoney.com/api/qt/stock/get?secid={BuildSecId(stock)}" +
                  $"&fields=f43,f44,f45,f46,f47,f48,f57,f58,f60,f86&invt=2";
        try
        {
            var json = await _http.GetStringAsync(url, ct).ConfigureAwait(false);
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Object)
                return null;

            // 东财价格放大了 100 倍（多数标的，按 f59 小数位还原；此处简化按 100 处理）
            double Get(string field, double scale = 100)
            {
                if (data.TryGetProperty(field, out var el) && el.ValueKind == JsonValueKind.Number)
                    return el.GetDouble() / scale;
                return 0;
            }
            string Name() =>
                data.TryGetProperty("f58", out var n) ? (n.GetString() ?? "") : "";

            return new RealtimeQuote
            {
                Name = Name(),
                Last = Get("f43"),
                High = Get("f44"),
                Low = Get("f45"),
                Open = Get("f46"),
                Volume = data.TryGetProperty("f47", out var v) && v.ValueKind == JsonValueKind.Number ? v.GetDouble() : 0,
                Amount = data.TryGetProperty("f48", out var a) && a.ValueKind == JsonValueKind.Number ? a.GetDouble() : 0,
                PreClose = Get("f60"),
            };
        }
        catch { return null; }
    }

    // ---- K 线 ----------------------------------------------------------
    public async Task<IReadOnlyList<Kline>> GetKlineAsync(StockInfo stock, KlinePeriod period, int count, CancellationToken ct = default)
    {
        int klt = period switch
        {
            KlinePeriod.Daily => 101,
            KlinePeriod.Weekly => 102,
            KlinePeriod.Monthly => 103,
            _ => 101,
        };
        int lmt = Math.Clamp(count, 30, 5000);

        var url = $"https://push2his.eastmoney.com/api/qt/stock/kline/get?" +
                  $"secid={BuildSecId(stock)}" +
                  $"&fields1=f1,f2,f3,f4,f5,f6" +
                  $"&fields2=f51,f52,f53,f54,f55,f56,f57,f58" +
                  $"&klt={klt}&fqt=1&end=20500101&lmt={lmt}";

        var json = await _http.GetStringAsync(url, ct).ConfigureAwait(false);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        if (!root.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Object)
            return Array.Empty<Kline>();
        if (!data.TryGetProperty("klines", out var klines) || klines.ValueKind != JsonValueKind.Array)
            return Array.Empty<Kline>();

        var list = new List<Kline>(klines.GetArrayLength());
        var ci = CultureInfo.InvariantCulture;
        foreach (var el in klines.EnumerateArray())
        {
            var line = el.GetString();
            if (string.IsNullOrEmpty(line)) continue;
            var p = line.Split(',');
            // f51 日期, f52 开, f53 收, f54 高, f55 低, f56 成交量(手), f57 成交额(元), f58 振幅%
            if (p.Length < 7) continue;
            if (!DateTime.TryParse(p[0], out var d)) continue;
            list.Add(new Kline
            {
                Date = d,
                Open = double.Parse(p[1], ci),
                Close = double.Parse(p[2], ci),
                High = double.Parse(p[3], ci),
                Low = double.Parse(p[4], ci),
                Volume = double.Parse(p[5], ci),
                Amount = double.Parse(p[6], ci),
            });
        }
        return list;
    }
}
