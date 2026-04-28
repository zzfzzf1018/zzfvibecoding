using System.Globalization;
using System.Net.Http;
using ComparetoolWpf.Models;
using Newtonsoft.Json.Linq;

namespace ComparetoolWpf.Services;

/// <summary>
/// 选股器：调用东方财富数据中心的“业绩报告”接口
/// (RPT_LICO_FN_CPD) 一次性获得多只股票最新财报关键指标，
/// 然后按 <see cref="ScreenerFilter"/> 进行客户端过滤。
///
/// 优点：单次请求即可获取数百只股票指标，适合做基本面初筛。
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
    /// 执行筛选。会按页拉取直到没有更多数据或达到上限。
    /// </summary>
    /// <param name="filter">筛选条件。</param>
    /// <param name="maxPages">最大翻页数（每页 PageSize 条），防止过多请求。</param>
    public async Task<List<ScreenerRow>> ScreenAsync(
        ScreenerFilter filter, int maxPages = 5, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(filter.ReportDate))
            throw new ArgumentException("必须指定 ReportDate。", nameof(filter));

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
        return result;
    }

    private async Task<List<ScreenerRow>> FetchPageAsync(
        ScreenerFilter f, int page, CancellationToken ct)
    {
        // 接口字段说明（业绩报告 RPT_LICO_FN_CPD）：
        //   SECURITY_CODE, SECURITY_NAME_ABBR, INDUSTRY, BASIC_EPS, TOTAL_OPERATE_INCOME,
        //   PARENT_NETPROFIT, WEIGHTAVG_ROE, GROSS_PROFIT_RATIO, YSTZ(营收同比), SJLTZ(净利同比), ...
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
        if (data == null) return list;

        foreach (var item in data.OfType<JObject>())
        {
            list.Add(new ScreenerRow
            {
                Code = item.Value<string>("SECURITY_CODE") ?? "",
                Name = item.Value<string>("SECURITY_NAME_ABBR") ?? "",
                Industry = item.Value<string>("INDUSTRY") ?? "",
                Eps = ParseDouble(item, "BASIC_EPS"),
                Revenue = ParseDouble(item, "TOTAL_OPERATE_INCOME"),
                NetProfit = ParseDouble(item, "PARENT_NETPROFIT"),
                Roe = AsRatio(ParseDouble(item, "WEIGHTAVG_ROE")),               // 接口返回百分数
                GrossMargin = AsRatio(ParseDouble(item, "GROSS_PROFIT_RATIO")),
                RevenueYoY = AsRatio(ParseDouble(item, "YSTZ")),
                NetProfitYoY = AsRatio(ParseDouble(item, "SJLTZ")),
                NetMargin = ComputeNetMargin(
                    ParseDouble(item, "PARENT_NETPROFIT"),
                    ParseDouble(item, "TOTAL_OPERATE_INCOME")),
            });
        }
        return list;
    }

    private static bool Pass(ScreenerRow r, ScreenerFilter f)
    {
        if (!string.IsNullOrEmpty(f.IndustryContains) &&
            (r.Industry == null || !r.Industry.Contains(f.IndustryContains))) return false;
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
