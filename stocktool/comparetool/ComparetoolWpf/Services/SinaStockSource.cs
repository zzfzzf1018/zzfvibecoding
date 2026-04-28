using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using ComparetoolWpf.Models;

namespace ComparetoolWpf.Services;

/// <summary>
/// 新浪财经 (sina.com.cn) 数据源。
///
/// 接口（公开 H5/Web）：
///   1. 搜索：https://suggest3.sinajs.cn/suggest/type=11,12,13,14,15&key=keyword
///      返回 JSONP 文本：var suggestvalue="11,A,SH600000,浦发银行,puhuayinhang,...";
///      多条用 ; 分隔，每条按逗号字段。type 字段：11=A股, 12=B股, 13=权证, 14=指数, 15=债券
///   2. 三大报表：新浪报表为 HTML/Excel，无 JSON 接口；本类不实现，
///      <see cref="GetReportsAsync"/> 抛出 <see cref="NotSupportedException"/>，
///      由 <see cref="CompositeStockSource"/> 自动降级到下一个数据源。
/// </summary>
public class SinaStockSource : IStockDataSource
{
    private readonly HttpClient _http;

    public string Name => "新浪财经";

    public SinaStockSource()
    {
        _http = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
        _http.DefaultRequestHeaders.UserAgent.ParseAdd(
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 ComparetoolWpf/1.0");
        _http.DefaultRequestHeaders.Referrer = new Uri("https://finance.sina.com.cn/");
    }

    public async Task<List<StockInfo>> SearchStocksAsync(string keyword, CancellationToken ct = default)
    {
        var list = new List<StockInfo>();
        if (string.IsNullOrWhiteSpace(keyword)) return list;

        var url = $"https://suggest3.sinajs.cn/suggest/type=11&key={Uri.EscapeDataString(keyword)}";
        // 新浪返回 GBK 编码
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        using var resp = await _http.GetAsync(url, ct);
        var bytes = await resp.Content.ReadAsByteArrayAsync(ct);
        string text;
        try { text = Encoding.GetEncoding("GBK").GetString(bytes); }
        catch { text = Encoding.UTF8.GetString(bytes); }

        // var suggestvalue="...";
        var m = Regex.Match(text, "\"(?<v>[^\"]*)\"");
        if (!m.Success) return list;
        var payload = m.Groups["v"].Value;
        if (string.IsNullOrEmpty(payload)) return list;

        foreach (var entry in payload.Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            var fields = entry.Split(',');
            if (fields.Length < 4) continue;
            // 期望：[type=11, A, SH600000, 浦发银行, ...]
            var fullCode = fields[2];          // 形如 sh600000 / sz000001
            var name = fields[3];
            if (fullCode.Length < 8) continue;
            var market = fullCode.Substring(0, 2).ToUpperInvariant();
            var code = fullCode.Substring(2);
            if (market != "SH" && market != "SZ") continue;
            if (code.Length != 6 || !code.All(char.IsDigit)) continue;
            list.Add(new StockInfo(code, name, market));
        }
        return list;
    }

    public Task<List<FinancialReport>> GetReportsAsync(
        StockInfo stock, ReportKind kind, ReportPeriodType periodType = ReportPeriodType.All,
        int pageSize = 20, CancellationToken ct = default)
    {
        // 新浪没有规整 JSON 报表接口；标记不支持以便降级
        throw new NotSupportedException("新浪财经数据源暂不支持三大报表（搜索可用）。");
    }
}
