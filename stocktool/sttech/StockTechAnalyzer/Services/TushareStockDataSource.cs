using System.Globalization;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using StockTechAnalyzer.Models;

namespace StockTechAnalyzer.Services;

/// <summary>
/// Tushare Pro HTTP 数据源（需要用户提供 token）。
/// 文档: https://tushare.pro/document/2
/// 搜索/实时报价沿用新浪（Tushare 无免费实时行情）。
/// </summary>
public sealed class TushareStockDataSource : IStockDataSource
{
    public string Name => "Tushare Pro";

    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(30) };
    private readonly SinaStockDataSource _fallback;
    private readonly string _token;

    private const string Endpoint = "http://api.waditu.com";

    public TushareStockDataSource(string token, SinaStockDataSource fallback)
    {
        _token = token ?? "";
        _fallback = fallback;
    }

    public Task<IReadOnlyList<StockInfo>> SearchAsync(string keyword, CancellationToken ct = default)
        => _fallback.SearchAsync(keyword, ct);

    public Task<RealtimeQuote?> GetQuoteAsync(StockInfo stock, CancellationToken ct = default)
        => _fallback.GetQuoteAsync(stock, ct);

    public async Task<IReadOnlyList<Kline>> GetKlineAsync(StockInfo stock, KlinePeriod period, int count, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_token))
            throw new InvalidOperationException("未配置 Tushare Token，请在 设置 中填写或切换至新浪数据源。");

        // ts_code 形如 600000.SH / 000001.SZ / 430047.BJ
        var suffix = stock.Market.ToUpperInvariant();
        var tsCode = $"{stock.Code}.{suffix}";

        string apiName = period switch
        {
            KlinePeriod.Daily => "daily",
            KlinePeriod.Weekly => "weekly",
            KlinePeriod.Monthly => "monthly",
            _ => "daily",
        };

        // 估算起始日期
        int days = period switch
        {
            KlinePeriod.Daily => count + 30,
            KlinePeriod.Weekly => count * 7 + 60,
            KlinePeriod.Monthly => count * 31 + 120,
            _ => count + 30,
        };
        var end = DateTime.Today;
        var start = end.AddDays(-days * 2);

        var payload = new
        {
            api_name = apiName,
            token = _token,
            @params = new Dictionary<string, string>
            {
                ["ts_code"] = tsCode,
                ["start_date"] = start.ToString("yyyyMMdd"),
                ["end_date"] = end.ToString("yyyyMMdd"),
            },
            fields = "trade_date,open,high,low,close,vol,amount",
        };

        using var resp = await _http.PostAsJsonAsync(Endpoint, payload, ct).ConfigureAwait(false);
        resp.EnsureSuccessStatusCode();
        await using var stream = await resp.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct).ConfigureAwait(false);

        var root = doc.RootElement;
        if (root.TryGetProperty("code", out var codeEl) && codeEl.GetInt32() != 0)
        {
            var msg = root.TryGetProperty("msg", out var msgEl) ? msgEl.GetString() : "未知错误";
            throw new InvalidOperationException($"Tushare 返回错误: {msg}");
        }

        var data = root.GetProperty("data");
        var fields = data.GetProperty("fields").EnumerateArray().Select(e => e.GetString()!).ToList();
        int iDate = fields.IndexOf("trade_date");
        int iOpen = fields.IndexOf("open");
        int iHigh = fields.IndexOf("high");
        int iLow = fields.IndexOf("low");
        int iClose = fields.IndexOf("close");
        int iVol = fields.IndexOf("vol");
        int iAmt = fields.IndexOf("amount");

        var list = new List<Kline>();
        foreach (var row in data.GetProperty("items").EnumerateArray())
        {
            var dStr = row[iDate].GetString()!;
            if (!DateTime.TryParseExact(dStr, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d))
                continue;
            list.Add(new Kline
            {
                Date = d,
                Open = GetD(row[iOpen]),
                High = GetD(row[iHigh]),
                Low = GetD(row[iLow]),
                Close = GetD(row[iClose]),
                Volume = GetD(row[iVol]),
                Amount = GetD(row[iAmt]),
            });
        }
        list.Sort((a, b) => a.Date.CompareTo(b.Date));
        return list.TakeLast(count).ToList();
    }

    private static double GetD(JsonElement e) => e.ValueKind switch
    {
        JsonValueKind.Number => e.GetDouble(),
        JsonValueKind.String => double.TryParse(e.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var v) ? v : 0,
        _ => 0,
    };
}
