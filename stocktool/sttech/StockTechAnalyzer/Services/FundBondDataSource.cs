using System.Net.Http;
using System.Text.Json;

namespace StockTechAnalyzer.Services;

/// <summary>
/// 东方财富 - 可转债 / ETF 列表查询。
/// 数据接口：push2.eastmoney.com/api/qt/clist/get
/// </summary>
internal sealed class FundBondDataSource
{
    private static readonly HttpClient _http = new(new HttpClientHandler { AutomaticDecompression = System.Net.DecompressionMethods.All });

    static FundBondDataSource()
    {
        if (!_http.DefaultRequestHeaders.UserAgent.Any())
            _http.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
    }

    public sealed record BondItem(string Code, string Name, string StockCode, string StockName,
        double Price, double ChangePct, double Premium, double ConversionPrice, string ListDate);

    public sealed record EtfItem(string Code, string Name, double Price, double ChangePct,
        double Amount, double TurnoverPct, double Pe);

    /// <summary>
    /// 获取可转债列表（沪深可转债）。
    /// 字段：f12 代码 / f14 名称 / f2 现价 / f3 涨跌% / f227 转股价 / f230 正股代码 /
    ///       f231 正股简称 / f232 转股溢价率 / f233 上市日期
    /// </summary>
    public async Task<IList<BondItem>> GetConvertibleBondsAsync(CancellationToken ct = default)
    {
        var url = "https://push2.eastmoney.com/api/qt/clist/get?pn=1&pz=500&po=1&np=1&" +
                  "fs=b:MK0354&" +
                  "fields=f12,f14,f2,f3,f227,f230,f231,f232,f233";
        var json = await _http.GetStringAsync(url, ct);
        return Parse(json, n =>
        {
            string code = Get(n, "f12");
            string name = Get(n, "f14");
            return new BondItem(
                code, name,
                Get(n, "f230"), Get(n, "f231"),
                GetD(n, "f2"), GetD(n, "f3"),
                GetD(n, "f232"), GetD(n, "f227"),
                Get(n, "f233"));
        });
    }

    /// <summary>
    /// 获取场内 ETF 列表。
    /// 字段：f12 代码 / f14 名称 / f2 现价 / f3 涨跌% / f6 成交额 / f8 换手率 / f9 PE
    /// </summary>
    public async Task<IList<EtfItem>> GetEtfsAsync(CancellationToken ct = default)
    {
        var url = "https://push2.eastmoney.com/api/qt/clist/get?pn=1&pz=500&po=1&np=1&" +
                  "fs=b:MK0021,b:MK0022,b:MK0023,b:MK0024&" +
                  "fields=f12,f14,f2,f3,f6,f8,f9";
        var json = await _http.GetStringAsync(url, ct);
        return Parse(json, n => new EtfItem(
            Get(n, "f12"), Get(n, "f14"),
            GetD(n, "f2"), GetD(n, "f3"),
            GetD(n, "f6"), GetD(n, "f8"), GetD(n, "f9")));
    }

    private static IList<T> Parse<T>(string json, Func<JsonElement, T> map)
    {
        var list = new List<T>();
        using var doc = JsonDocument.Parse(json);
        if (!doc.RootElement.TryGetProperty("data", out var data) ||
            data.ValueKind != JsonValueKind.Object ||
            !data.TryGetProperty("diff", out var diff) ||
            diff.ValueKind != JsonValueKind.Array) return list;
        foreach (var n in diff.EnumerateArray())
        {
            try { list.Add(map(n)); }
            catch { /* skip */ }
        }
        return list;
    }

    private static string Get(JsonElement n, string key)
    {
        if (!n.TryGetProperty(key, out var v)) return "";
        return v.ValueKind switch
        {
            JsonValueKind.String => v.GetString() ?? "",
            JsonValueKind.Number => v.GetRawText(),
            _ => "",
        };
    }

    private static double GetD(JsonElement n, string key)
    {
        if (!n.TryGetProperty(key, out var v)) return 0;
        if (v.ValueKind == JsonValueKind.Number) return v.GetDouble();
        if (v.ValueKind == JsonValueKind.String && double.TryParse(v.GetString(), out var d)) return d;
        return 0;
    }
}
