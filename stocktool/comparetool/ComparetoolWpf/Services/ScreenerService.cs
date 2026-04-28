using System.Globalization;
using System.Net.Http;
using ComparetoolWpf.Models;
using Newtonsoft.Json.Linq;

namespace ComparetoolWpf.Services;

/// <summary>
/// 选股器：调用东方财富数据中心的 “业绩报告” 接口
/// (RPT_LICO_FN_CPD) 一次性获得多只股票最新财报关键指标，
/// 然后按 <see cref="ScreenerFilter"/> 进行客户端过滤。
///
/// 字段口径（实测 2026 年 4 月）：
///   SECURITY_CODE          股票代码
///   SECURITY_NAME_ABBR     名称
///   PUBLISHNAME / BOARD_NAME 行业(申万二级板块名)
///   BASIC_EPS              EPS
///   TOTAL_OPERATE_INCOME   营业总收入(元)
///   PARENT_NETPROFIT       归母净利润(元)
///   WEIGHTAVG_ROE          ROE 加权(%)
///   XSMLL                  销售毛利率(%)
///   YSTZ                   营收同比(%)
///   SJLTZ                  净利同比(%)
///
/// 估值数据通过 RPT_VALUEANALYSIS_DET 接口按需附加。
/// </summary>
public class ScreenerService
{
    private readonly HttpClient _http;

    public ScreenerService()
    {
        _http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        _http.DefaultRequestHeaders.UserAgent.ParseAdd(
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 ComparetoolWpf/1.0");
        _http.DefaultRequestHeaders.Referrer = new Uri("https://data.eastmoney.com/");
    }

    /// <summary>
    /// 把任意日期对齐到最近的"已过去"季报日（3-31 / 6-30 / 9-30 / 12-31）。
    /// 例如 2026-10-07 → 2026-09-30；2026-04-15 → 2026-03-31。
    /// </summary>
    public static DateTime SnapToQuarterEnd(DateTime d)
    {
        var candidates = new[]
        {
            new DateTime(d.Year, 12, 31),
            new DateTime(d.Year, 9, 30),
            new DateTime(d.Year, 6, 30),
            new DateTime(d.Year, 3, 31),
            new DateTime(d.Year - 1, 12, 31),
        };
        // 取 <= d 的最大候选
        return candidates.Where(c => c <= d).Max();
    }

    /// <summary>
    /// 根据当前日期推荐"最近一份已披露报告期"。
    /// 披露窗口口径（保守）：年报4-30、一季报4-30、中报8-31、三季报10-31。
    /// </summary>
    public static DateTime RecommendReportDate(DateTime today)
    {
        // 4-30 之后：去年年报已出 + 一季报已出 → 取一季报；前后取上一季
        // 5-1 ~ 8-31：仍以一季报为新
        // 9-1 ~ 10-31：取中报
        // 11-1 ~ 次年4-30：取三季报
        var y = today.Year;
        if (today >= new DateTime(y, 11, 1)) return new DateTime(y, 9, 30);
        if (today >= new DateTime(y, 9, 1))  return new DateTime(y, 6, 30);
        if (today >= new DateTime(y, 5, 1))  return new DateTime(y, 3, 31);
        // 1-1 ~ 4-30：用上一年的三季报作为"最近已披露"
        return new DateTime(y - 1, 9, 30);
    }

    /// <summary>
    /// 执行筛选。会按页拉取直到没有更多数据或达到上限。
    /// </summary>
    public async Task<List<ScreenerRow>> ScreenAsync(
        ScreenerFilter filter, int maxPages = 5, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(filter.ReportDate))
            throw new ArgumentException("必须指定 ReportDate。", nameof(filter));

        Logger.Info($"Screener 查询: date={filter.ReportDate}, industries={(filter.IndustryAnyOf == null ? "-" : string.Join("|", filter.IndustryAnyOf))}, maxPages={maxPages}");

        var result = new List<ScreenerRow>();
        for (int page = 1; page <= maxPages; page++)
        {
            var batch = await FetchPageAsync(filter, page, ct);
            if (batch.Count == 0) break;
            foreach (var row in batch)
            {
                if (Pass(row, filter)) result.Add(row);
            }
            if (batch.Count < filter.PageSize) break;
        }

        if (filter.IncludeValuation && result.Count > 0)
        {
            await EnrichValuationAsync(result, ct);
            // 估值过滤
            result = result.Where(r => PassValuation(r, filter)).ToList();
        }

        Logger.Info($"Screener 命中 {result.Count} 只");
        return result;
    }

    private async Task<List<ScreenerRow>> FetchPageAsync(
        ScreenerFilter f, int page, CancellationToken ct)
    {
        var filterStr = $"(REPORTDATE='{f.ReportDate}')";
        var url = "https://datacenter-web.eastmoney.com/api/data/v1/get" +
                  $"?sortColumns=UPDATE_DATE,SECURITY_CODE&sortTypes=-1,-1" +
                  $"&pageSize={f.PageSize}&pageNumber={page}" +
                  $"&reportName=RPT_LICO_FN_CPD&columns=ALL" +
                  $"&filter={Uri.EscapeDataString(filterStr)}" +
                  $"&source=WEB&client=WEB";

        var json = await _http.GetStringAsync(url, ct);
        var jo = JObject.Parse(json);
        JArray? data = null;
        if (jo["result"] is JObject ro) data = ro["data"] as JArray;
        var list = new List<ScreenerRow>();
        if (data == null || data.Count == 0) return list;

        foreach (var item in data.OfType<JObject>())
        {
            // 行业字段优先用 PUBLISHNAME(申万二级板块名)，回退 BOARD_NAME / TRADE_MARKET
            var industry = item.Value<string>("PUBLISHNAME")
                           ?? item.Value<string>("BOARD_NAME")
                           ?? string.Empty;
            list.Add(new ScreenerRow
            {
                Code = item.Value<string>("SECURITY_CODE") ?? "",
                Name = item.Value<string>("SECURITY_NAME_ABBR") ?? "",
                Industry = industry,
                Eps = ParseDouble(item, "BASIC_EPS"),
                Revenue = ParseDouble(item, "TOTAL_OPERATE_INCOME"),
                NetProfit = ParseDouble(item, "PARENT_NETPROFIT"),
                Roe = AsRatio(ParseDouble(item, "WEIGHTAVG_ROE")),
                GrossMargin = AsRatio(ParseDouble(item, "XSMLL")),    // 销售毛利率
                RevenueYoY = AsRatio(ParseDouble(item, "YSTZ")),
                NetProfitYoY = AsRatio(ParseDouble(item, "SJLTZ")),
                NetMargin = ComputeNetMargin(
                    ParseDouble(item, "PARENT_NETPROFIT"),
                    ParseDouble(item, "TOTAL_OPERATE_INCOME")),
            });
        }
        return list;
    }

    /// <summary>批量补充 PE/PB/PS/股息率/总市值。每次最多 50 只一批。</summary>
    private async Task EnrichValuationAsync(List<ScreenerRow> rows, CancellationToken ct)
    {
        const int batchSize = 50;
        for (int start = 0; start < rows.Count; start += batchSize)
        {
            var slice = rows.Skip(start).Take(batchSize).ToList();
            var codes = string.Join(",", slice.Select(r => r.Code));
            var filterStr = $"(SECURITY_CODE in (\"{string.Join("\",\"", slice.Select(r => r.Code))}\"))";
            var url = "https://datacenter-web.eastmoney.com/api/data/v1/get" +
                      $"?reportName=RPT_VALUEANALYSIS_DET&columns=ALL" +
                      $"&pageSize={batchSize}&pageNumber=1" +
                      $"&filter={Uri.EscapeDataString(filterStr)}" +
                      $"&source=WEB&client=WEB";
            try
            {
                var json = await _http.GetStringAsync(url, ct);
                var jo = JObject.Parse(json);
                if (jo["result"] is not JObject ro) continue;
                if (ro["data"] is not JArray data) continue;
                var byCode = slice.ToDictionary(r => r.Code, r => r);
                foreach (var item in data.OfType<JObject>())
                {
                    var code = item.Value<string>("SECURITY_CODE");
                    if (code == null || !byCode.TryGetValue(code, out var row)) continue;
                    row.PE = ParseDouble(item, "PE_TTM");
                    row.PB = ParseDouble(item, "PB_MRQ");
                    row.PS = ParseDouble(item, "PS_TTM");
                    row.DividendYield = ParseDouble(item, "DIVIDENDYIELD");  // 已是 %
                    row.TotalMarketCap = ParseDouble(item, "TOTAL_MARKET_CAP");
                }
            }
            catch (Exception ex)
            {
                Logger.Warn($"估值补充失败 batch[{start}]: {ex.Message}");
            }
        }
    }

    private static bool Pass(ScreenerRow r, ScreenerFilter f)
    {
        if (!string.IsNullOrEmpty(f.IndustryContains) &&
            (string.IsNullOrEmpty(r.Industry) || !r.Industry.Contains(f.IndustryContains))) return false;
        if (f.IndustryAnyOf != null && f.IndustryAnyOf.Count > 0)
        {
            if (string.IsNullOrEmpty(r.Industry)) return false;
            bool any = false;
            foreach (var kw in f.IndustryAnyOf)
            {
                if (!string.IsNullOrEmpty(kw) && r.Industry.Contains(kw)) { any = true; break; }
            }
            if (!any) return false;
        }
        if (f.RoeMin.HasValue && (!r.Roe.HasValue || r.Roe < f.RoeMin)) return false;
        if (f.GrossMarginMin.HasValue && (!r.GrossMargin.HasValue || r.GrossMargin < f.GrossMarginMin)) return false;
        if (f.NetMarginMin.HasValue && (!r.NetMargin.HasValue || r.NetMargin < f.NetMarginMin)) return false;
        if (f.RevenueYoYMin.HasValue && (!r.RevenueYoY.HasValue || r.RevenueYoY < f.RevenueYoYMin)) return false;
        if (f.NetProfitYoYMin.HasValue && (!r.NetProfitYoY.HasValue || r.NetProfitYoY < f.NetProfitYoYMin)) return false;
        if (f.EpsMin.HasValue && (!r.Eps.HasValue || r.Eps < f.EpsMin)) return false;
        return true;
    }

    private static bool PassValuation(ScreenerRow r, ScreenerFilter f)
    {
        if (f.PeMax.HasValue && (!r.PE.HasValue || r.PE <= 0 || r.PE > f.PeMax)) return false;
        if (f.PbMax.HasValue && (!r.PB.HasValue || r.PB > f.PbMax)) return false;
        if (f.PsMax.HasValue && (!r.PS.HasValue || r.PS > f.PsMax)) return false;
        if (f.DividendYieldMin.HasValue &&
            (!r.DividendYield.HasValue || r.DividendYield < f.DividendYieldMin)) return false;
        return true;
    }

    private static double? ParseDouble(JObject item, string key)
    {
        var token = item[key];
        if (token == null || token.Type == JTokenType.Null) return null;
        if (double.TryParse(token.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var v))
            return v;
        return null;
    }

    /// <summary>东方财富的同比/ROE/毛利率字段是百分数（如 12.34），转成小数。</summary>
    private static double? AsRatio(double? v) => v.HasValue ? v.Value / 100.0 : null;

    private static double? ComputeNetMargin(double? np, double? rev)
    {
        if (!np.HasValue || !rev.HasValue || Math.Abs(rev.Value) < 1e-9) return null;
        return np.Value / rev.Value;
    }
}
