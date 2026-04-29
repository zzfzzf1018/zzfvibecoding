using System.Globalization;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using StockTechAnalyzer.Models;

namespace StockTechAnalyzer.Services;

/// <summary>
/// 新浪财经数据源（A股，免费、无需 token）。
/// 注意：接口为非官方公开端点，仅用于学习/研究。
/// </summary>
public sealed class SinaStockDataSource : IStockDataSource
{
    public string Name => "新浪财经";

    private readonly HttpClient _http;

    public SinaStockDataSource()
    {
        _http = new HttpClient(new HttpClientHandler
        {
            AutomaticDecompression = System.Net.DecompressionMethods.All,
        })
        {
            Timeout = TimeSpan.FromSeconds(15),
        };
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 StockTechAnalyzer/1.0");
        _http.DefaultRequestHeaders.Referrer = new Uri("https://finance.sina.com.cn/");
    }

    // ---- 搜索 ----------------------------------------------------------
    public async Task<IReadOnlyList<StockInfo>> SearchAsync(string keyword, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(keyword)) return Array.Empty<StockInfo>();

        // 若是纯数字代码，直接构造
        if (Regex.IsMatch(keyword.Trim(), @"^\d{6}$"))
        {
            var info = BuildFromCode(keyword.Trim());
            // 拉一下名称
            var quote = await GetQuoteAsync(info, ct).ConfigureAwait(false);
            if (quote != null && !string.IsNullOrEmpty(quote.Name))
                info.Name = quote.Name;
            return new[] { info };
        }

        var url = $"https://suggest3.sinajs.cn/suggest/type=11,12,13,14,15&key={Uri.EscapeDataString(keyword)}";
        var bytes = await _http.GetByteArrayAsync(url, ct).ConfigureAwait(false);
        var text = Encoding.GetEncoding("GB18030").GetString(bytes);
        // 形如: var suggestvalue="平安银行,11,000001,sz000001,平安银行,...;..."
        var m = Regex.Match(text, "\"(.*)\"", RegexOptions.Singleline);
        if (!m.Success) return Array.Empty<StockInfo>();
        var payload = m.Groups[1].Value;
        if (string.IsNullOrEmpty(payload)) return Array.Empty<StockInfo>();

        var list = new List<StockInfo>();
        foreach (var row in payload.Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = row.Split(',');
            if (parts.Length < 5) continue;
            var fullCode = parts[3];
            if (string.IsNullOrEmpty(fullCode) || fullCode.Length < 3) continue;
            var market = fullCode.Substring(0, 2);
            if (market != "sh" && market != "sz" && market != "bj") continue;
            list.Add(new StockInfo
            {
                Name = parts[4],
                Code = parts[2],
                FullCode = fullCode,
                Market = market,
            });
            if (list.Count >= 30) break;
        }
        return list;
    }

    public static StockInfo BuildFromCode(string code)
    {
        string market;
        if (code.StartsWith('6') || code.StartsWith('9')) market = "sh";
        else if (code.StartsWith('0') || code.StartsWith('3')) market = "sz";
        else if (code.StartsWith('4') || code.StartsWith('8')) market = "bj";
        else market = "sh";
        return new StockInfo { Code = code, Market = market, FullCode = market + code, Name = code };
    }

    // ---- 实时报价 -------------------------------------------------------
    public async Task<RealtimeQuote?> GetQuoteAsync(StockInfo stock, CancellationToken ct = default)
    {
        var url = $"https://hq.sinajs.cn/list={stock.FullCode}";
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, url);
            req.Headers.Referrer = new Uri("https://finance.sina.com.cn/");
            using var resp = await _http.SendAsync(req, ct).ConfigureAwait(false);
            var bytes = await resp.Content.ReadAsByteArrayAsync(ct).ConfigureAwait(false);
            var text = Encoding.GetEncoding("GB18030").GetString(bytes);
            var m = Regex.Match(text, "\"(.*)\"");
            if (!m.Success) return null;
            var fields = m.Groups[1].Value.Split(',');
            if (fields.Length < 10) return null;
            double D(int i) => double.TryParse(fields[i], NumberStyles.Any, CultureInfo.InvariantCulture, out var v) ? v : 0;
            return new RealtimeQuote
            {
                Name = fields[0],
                Open = D(1),
                PreClose = D(2),
                Last = D(3),
                High = D(4),
                Low = D(5),
                Volume = D(8),
                Amount = D(9),
            };
        }
        catch { return null; }
    }

    // ---- K 线 ----------------------------------------------------------
    public async Task<IReadOnlyList<Kline>> GetKlineAsync(StockInfo stock, KlinePeriod period, int count, CancellationToken ct = default)
    {
        // 新浪日线接口 scale=240。周/月线通过日线聚合（更稳定）。
        // 若是周/月线，多取些日线再聚合
        int dayCount = period switch
        {
            KlinePeriod.Daily => Math.Max(count, 30),
            KlinePeriod.Weekly => Math.Max(count * 6, 200),
            KlinePeriod.Monthly => Math.Max(count * 25, 500),
            _ => count,
        };
        dayCount = Math.Min(dayCount, 1023); // 接口上限

        var url = $"https://quotes.sina.cn/cn/api/json_v2.php/CN_MarketDataService.getKLineData" +
                  $"?symbol={stock.FullCode}&scale=240&ma=no&datalen={dayCount}";

        var json = await _http.GetStringAsync(url, ct).ConfigureAwait(false);
        // 返回是非严格 JSON（key 不带引号），用正则解析
        var daily = ParseSinaKline(json);
        if (daily.Count == 0) return daily;

        return period switch
        {
            KlinePeriod.Daily => daily.TakeLast(count).ToList(),
            KlinePeriod.Weekly => Aggregate(daily, weekly: true).TakeLast(count).ToList(),
            KlinePeriod.Monthly => Aggregate(daily, weekly: false).TakeLast(count).ToList(),
            _ => daily,
        };
    }

    private static List<Kline> ParseSinaKline(string text)
    {
        var list = new List<Kline>();
        // 兼容 [{day:"2024-01-02",open:"...",...}, ...]
        // 用宽松正则
        var rx = new Regex(@"\{[^{}]*day:""(?<d>[^""]+)""[^{}]*open:""(?<o>[^""]+)""[^{}]*high:""(?<h>[^""]+)""[^{}]*low:""(?<l>[^""]+)""[^{}]*close:""(?<c>[^""]+)""[^{}]*volume:""(?<v>[^""]+)""[^{}]*\}");
        foreach (Match m in rx.Matches(text))
        {
            if (!DateTime.TryParse(m.Groups["d"].Value, out var d)) continue;
            list.Add(new Kline
            {
                Date = d,
                Open = ToD(m.Groups["o"].Value),
                High = ToD(m.Groups["h"].Value),
                Low = ToD(m.Groups["l"].Value),
                Close = ToD(m.Groups["c"].Value),
                Volume = ToD(m.Groups["v"].Value),
            });
        }
        // 若上面正则失败，尝试标准 JSON
        if (list.Count == 0)
        {
            try
            {
                // 给 key 加上引号
                var fixedJson = Regex.Replace(text, "([\\{,])(\\s*)([a-zA-Z_][a-zA-Z0-9_]*)\\s*:", "$1\"$3\":");
                using var doc = JsonDocument.Parse(fixedJson);
                foreach (var el in doc.RootElement.EnumerateArray())
                {
                    list.Add(new Kline
                    {
                        Date = DateTime.Parse(el.GetProperty("day").GetString()!),
                        Open = ToD(el.GetProperty("open").GetString()!),
                        High = ToD(el.GetProperty("high").GetString()!),
                        Low = ToD(el.GetProperty("low").GetString()!),
                        Close = ToD(el.GetProperty("close").GetString()!),
                        Volume = ToD(el.GetProperty("volume").GetString()!),
                    });
                }
            }
            catch { /* 忽略，返回空 */ }
        }
        return list;
    }

    private static double ToD(string s) =>
        double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var v) ? v : 0;

    /// <summary>
    /// 将日 K 聚合为周/月 K。
    /// </summary>
    private static List<Kline> Aggregate(List<Kline> daily, bool weekly)
    {
        var result = new List<Kline>();
        if (daily.Count == 0) return result;

        Kline? cur = null;
        int curKey = int.MinValue;

        foreach (var d in daily)
        {
            int key;
            if (weekly)
            {
                var ci = CultureInfo.InvariantCulture;
                int week = ci.Calendar.GetWeekOfYear(d.Date, CalendarWeekRule.FirstFourDayWeek, DayOfWeek.Monday);
                key = d.Date.Year * 100 + week;
            }
            else
            {
                key = d.Date.Year * 100 + d.Date.Month;
            }

            if (cur == null || key != curKey)
            {
                if (cur != null) result.Add(cur);
                cur = new Kline
                {
                    Date = d.Date,
                    Open = d.Open,
                    High = d.High,
                    Low = d.Low,
                    Close = d.Close,
                    Volume = d.Volume,
                    Amount = d.Amount,
                };
                curKey = key;
            }
            else
            {
                cur.High = Math.Max(cur.High, d.High);
                cur.Low = Math.Min(cur.Low, d.Low);
                cur.Close = d.Close;
                cur.Volume += d.Volume;
                cur.Amount += d.Amount;
                cur.Date = d.Date; // 用区间最后一日代表
            }
        }
        if (cur != null) result.Add(cur);
        return result;
    }
}
