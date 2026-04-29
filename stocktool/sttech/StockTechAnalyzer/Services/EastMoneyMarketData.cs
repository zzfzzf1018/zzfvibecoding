using System.Globalization;
using System.Net.Http;
using System.Text;
using System.Text.Json;

namespace StockTechAnalyzer.Services;

/// <summary>
/// 东财市场宽度数据：龙虎榜 / 北向资金 / 申万一级行业 / 概念板块涨跌幅。
/// 公开接口，免费、无 token。
/// </summary>
public sealed class EastMoneyMarketData
{
    private readonly HttpClient _http;
    public EastMoneyMarketData()
    {
        _http = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 StockTechAnalyzer/1.0");
        _http.DefaultRequestHeaders.Referrer = new Uri("https://data.eastmoney.com/");
    }

    // ---- 龙虎榜 ----
    public sealed record LhbItem(string Date, string Code, string Name, double ChangePct,
        double NetBuy, double TurnoverPct, string Reason);

    public async Task<IReadOnlyList<LhbItem>> GetLhbAsync(DateTime? day = null, CancellationToken ct = default)
    {
        var d = (day ?? DateTime.Today).ToString("yyyy-MM-dd");
        var url = "https://datacenter-web.eastmoney.com/api/data/v1/get?" +
                  "sortColumns=SECURITY_CODE&sortTypes=1&pageSize=100&pageNumber=1" +
                  "&reportName=RPT_DAILYBILLBOARD_DETAILSNEW&columns=ALL" +
                  $"&filter=(TRADE_DATE%3E%3D%27{d}%27)(TRADE_DATE%3C%3D%27{d}%27)";
        try
        {
            var json = await _http.GetStringAsync(url, ct).ConfigureAwait(false);
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("result", out var res) || res.ValueKind != JsonValueKind.Object)
                return Array.Empty<LhbItem>();
            if (!res.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Array)
                return Array.Empty<LhbItem>();

            var list = new List<LhbItem>();
            foreach (var r in data.EnumerateArray())
            {
                string Get(string k) => r.TryGetProperty(k, out var v) && v.ValueKind == JsonValueKind.String ? (v.GetString() ?? "") : "";
                double D(string k)
                {
                    if (!r.TryGetProperty(k, out var v)) return 0;
                    return v.ValueKind == JsonValueKind.Number ? v.GetDouble()
                         : v.ValueKind == JsonValueKind.String && double.TryParse(v.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var d) ? d
                         : 0;
                }
                list.Add(new LhbItem(
                    Get("TRADE_DATE")[..Math.Min(10, Get("TRADE_DATE").Length)],
                    Get("SECURITY_CODE"),
                    Get("SECURITY_NAME_ABBR"),
                    D("CHANGE_RATE"),
                    D("BILLBOARD_NET_AMT") / 1e4,        // 转万元
                    D("ACCUM_AMOUNT") > 0 ? D("BILLBOARD_NET_AMT") / D("ACCUM_AMOUNT") * 100 : 0,
                    Get("EXPLANATION")
                ));
            }
            return list;
        }
        catch { return Array.Empty<LhbItem>(); }
    }

    // ---- 北向资金（最近 N 个交易日累计净流入）----
    public sealed record NorthFlow(DateTime Date, double HuGuTongNet, double ShenGuTongNet, double TotalNet);

    public async Task<IReadOnlyList<NorthFlow>> GetNorthFlowAsync(int days = 60, CancellationToken ct = default)
    {
        // 接口示例：lt=1=日, fields2: f51 日期 f52 沪股通 f54 深股通 f56 北向合计
        var url = $"https://push2his.eastmoney.com/api/qt/kamt.kline/get?fields1=f1,f3,f5&fields2=f51,f52,f54,f56&klt=101&lmt={days}";
        try
        {
            var json = await _http.GetStringAsync(url, ct).ConfigureAwait(false);
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("data", out var data)) return Array.Empty<NorthFlow>();
            if (!data.TryGetProperty("s2n", out var arr) || arr.ValueKind != JsonValueKind.Array)
                return Array.Empty<NorthFlow>();
            var ci = CultureInfo.InvariantCulture;
            var list = new List<NorthFlow>();
            foreach (var el in arr.EnumerateArray())
            {
                var s = el.GetString();
                if (string.IsNullOrEmpty(s)) continue;
                var p = s.Split(',');
                if (p.Length < 4) continue;
                if (!DateTime.TryParse(p[0], out var d)) continue;
                list.Add(new NorthFlow(d,
                    double.Parse(p[1], ci),
                    double.Parse(p[2], ci),
                    double.Parse(p[3], ci)));
            }
            return list;
        }
        catch { return Array.Empty<NorthFlow>(); }
    }

    // ---- 行业 / 概念板块涨跌（用于热力图）----
    public sealed record SectorItem(string Code, string Name, double ChangePct, double Amount);

    public async Task<IReadOnlyList<SectorItem>> GetSectorsAsync(bool concept = false, CancellationToken ct = default)
    {
        // 行业: m:90+t:2  概念: m:90+t:3
        string fs = concept ? "m:90+t:3" : "m:90+t:2";
        var url = $"https://push2.eastmoney.com/api/qt/clist/get?pn=1&pz=200&po=1&np=1&fltt=2&invt=2&fid=f3&fs={fs}&fields=f12,f14,f3,f6";
        try
        {
            var json = await _http.GetStringAsync(url, ct).ConfigureAwait(false);
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Object)
                return Array.Empty<SectorItem>();
            if (!data.TryGetProperty("diff", out var arr) || arr.ValueKind != JsonValueKind.Array)
                return Array.Empty<SectorItem>();
            var list = new List<SectorItem>();
            foreach (var el in arr.EnumerateArray())
            {
                string code = el.TryGetProperty("f12", out var v1) ? (v1.ValueKind == JsonValueKind.String ? v1.GetString()! : v1.ToString()) : "";
                string name = el.TryGetProperty("f14", out var v2) ? (v2.GetString() ?? "") : "";
                double pct = el.TryGetProperty("f3", out var v3) && v3.ValueKind == JsonValueKind.Number ? v3.GetDouble() : 0;
                double amt = el.TryGetProperty("f6", out var v4) && v4.ValueKind == JsonValueKind.Number ? v4.GetDouble() : 0;
                if (string.IsNullOrEmpty(name)) continue;
                list.Add(new SectorItem(code, name, pct, amt));
            }
            return list;
        }
        catch { return Array.Empty<SectorItem>(); }
    }
}
