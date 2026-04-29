using System.Globalization;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using StockTechAnalyzer.Models;

namespace StockTechAnalyzer.Services;

/// <summary>
/// 东方财富额外接口：基本面快照 + 资金流向。
/// </summary>
public sealed class EastMoneyExtras
{
    private readonly HttpClient _http;

    public EastMoneyExtras()
    {
        _http = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 StockTechAnalyzer/1.0");
        _http.DefaultRequestHeaders.Referrer = new Uri("https://quote.eastmoney.com/");
    }

    public sealed record Fundamentals(
        string Name, double Pe, double Pb, double TurnoverPct, double TotalMarketCap,
        double FloatMarketCap, double Roe, double Eps, double DividendYield, double VolumeRatio);

    public sealed record MoneyFlow(
        double MainNet,         // 主力净流入
        double SuperLargeNet,   // 超大单净
        double LargeNet,        // 大单净
        double MediumNet,       // 中单净
        double SmallNet);       // 小单净

    private static string SecId(StockInfo s) =>
        s.Market == "sh" ? "1." + s.Code : "0." + s.Code;

    public async Task<Fundamentals?> GetFundamentalsAsync(StockInfo stock, CancellationToken ct = default)
    {
        // 字段：f43现价 f58名称 f162PE_TTM f167PB f168换手率 f116总市值 f117流通市值
        // f173ROE f55EPS f86股息率 f50量比
        var url = $"https://push2.eastmoney.com/api/qt/stock/get?secid={SecId(stock)}" +
                  "&fields=f43,f50,f55,f58,f86,f116,f117,f162,f167,f168,f173&invt=2";
        try
        {
            var json = await _http.GetStringAsync(url, ct).ConfigureAwait(false);
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Object)
                return null;

            double D(string k, double scale = 100)
            {
                if (data.TryGetProperty(k, out var el) && el.ValueKind == JsonValueKind.Number)
                    return el.GetDouble() / scale;
                return double.NaN;
            }
            string Name() => data.TryGetProperty("f58", out var n) ? (n.GetString() ?? "") : "";

            return new Fundamentals(
                Name(),
                Pe: D("f162"),
                Pb: D("f167"),
                TurnoverPct: D("f168"),
                TotalMarketCap: D("f116", 1),
                FloatMarketCap: D("f117", 1),
                Roe: D("f173"),
                Eps: D("f55"),
                DividendYield: D("f86"),
                VolumeRatio: D("f50")
            );
        }
        catch { return null; }
    }

    public async Task<MoneyFlow?> GetMoneyFlowAsync(StockInfo stock, CancellationToken ct = default)
    {
        // 资金流接口字段：f62 主力净额 f66/72/78/84 超大/大/中/小净额 f184 主力净占比
        var url = $"https://push2.eastmoney.com/api/qt/stock/get?secid={SecId(stock)}" +
                  "&fields=f62,f66,f72,f78,f84&invt=2";
        try
        {
            var json = await _http.GetStringAsync(url, ct).ConfigureAwait(false);
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Object)
                return null;
            double D(string k)
            {
                if (data.TryGetProperty(k, out var el) && el.ValueKind == JsonValueKind.Number)
                    return el.GetDouble();
                return 0;
            }
            return new MoneyFlow(D("f62"), D("f66"), D("f72"), D("f78"), D("f84"));
        }
        catch { return null; }
    }

    public static string FormatFundamentals(Fundamentals? f)
    {
        if (f == null) return "（无基本面数据。该接口主要支持 A 股个股，指数/部分品种可能无数据。）";
        var sb = new StringBuilder();
        sb.AppendLine("════════════ 基本面快照 ════════════");
        sb.AppendLine();
        sb.AppendLine($"  · 名称           {f.Name}");
        sb.AppendLine($"  · 市盈率 PE-TTM  {Fmt(f.Pe)}      {PeDesc(f.Pe)}");
        sb.AppendLine($"  · 市净率 PB      {Fmt(f.Pb)}      {PbDesc(f.Pb)}");
        sb.AppendLine($"  · ROE            {FmtPct(f.Roe)}");
        sb.AppendLine($"  · 每股收益 EPS   {Fmt(f.Eps)}");
        sb.AppendLine($"  · 股息率         {FmtPct(f.DividendYield)}");
        sb.AppendLine($"  · 换手率(今日)   {FmtPct(f.TurnoverPct)}");
        sb.AppendLine($"  · 量比           {Fmt(f.VolumeRatio)}        {VrDesc(f.VolumeRatio)}");
        sb.AppendLine($"  · 总市值         {FmtMoney(f.TotalMarketCap)}");
        sb.AppendLine($"  · 流通市值       {FmtMoney(f.FloatMarketCap)}");
        return sb.ToString();
    }

    public static string FormatMoneyFlow(MoneyFlow? mf)
    {
        if (mf == null) return "（无资金流向数据。）";
        var sb = new StringBuilder();
        sb.AppendLine("════════════ 今日资金流向 ════════════");
        sb.AppendLine();
        sb.AppendLine($"  · 主力净额      {Money(mf.MainNet)}    {NetDesc(mf.MainNet)}");
        sb.AppendLine($"      ├─ 超大单   {Money(mf.SuperLargeNet)}    （机构/大资金）");
        sb.AppendLine($"      └─ 大单     {Money(mf.LargeNet)}    （游资/大户）");
        sb.AppendLine($"  · 中单          {Money(mf.MediumNet)}    （中户）");
        sb.AppendLine($"  · 小单          {Money(mf.SmallNet)}    （散户）");
        sb.AppendLine();
        sb.AppendLine("解读：主力净额为正=大资金净买入（看多）；负=净卖出（出货）。");
        sb.AppendLine("如散户净买入而主力净卖出，常被称为\"主力派发\"，需警惕。");
        return sb.ToString();
    }

    private static string Fmt(double v) => double.IsNaN(v) ? "—" : v.ToString("F2", CultureInfo.InvariantCulture);
    private static string FmtPct(double v) => double.IsNaN(v) ? "—" : (v.ToString("F2") + "%");
    private static string FmtMoney(double v) => double.IsNaN(v) || v == 0 ? "—" : (v >= 1e8 ? $"{v / 1e8:F2} 亿" : $"{v / 1e4:F2} 万");
    private static string Money(double v) => v == 0 ? "—" : (Math.Abs(v) >= 1e8 ? $"{v / 1e8:+0.00;-0.00;0} 亿" : $"{v / 1e4:+0.0;-0.0;0} 万");

    private static string PeDesc(double pe) => double.IsNaN(pe) || pe <= 0 ? "（亏损/无意义）"
        : pe < 15 ? "（偏低，传统行业常见）"
        : pe < 30 ? "（合理）"
        : pe < 60 ? "（偏高，成长股常见）"
        : "（高估，需谨慎）";

    private static string PbDesc(double pb) => double.IsNaN(pb) || pb <= 0 ? "—"
        : pb < 1 ? "（破净）"
        : pb < 2 ? "（较低）"
        : pb < 5 ? "（中等）"
        : "（偏高）";

    private static string VrDesc(double vr) => double.IsNaN(vr) ? ""
        : vr > 2 ? "（明显放量，关注度高）"
        : vr > 1.2 ? "（放量）"
        : vr > 0.8 ? "（正常）"
        : "（缩量）";

    private static string NetDesc(double v) => v > 0 ? "↑ 净流入" : v < 0 ? "↓ 净流出" : "持平";
}
