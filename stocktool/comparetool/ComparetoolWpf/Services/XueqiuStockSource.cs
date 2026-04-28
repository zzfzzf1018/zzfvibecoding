using System.Globalization;
using System.Net;
using System.Net.Http;
using ComparetoolWpf.Models;
using Newtonsoft.Json.Linq;

namespace ComparetoolWpf.Services;

/// <summary>
/// 雪球 (xueqiu.com) 数据源。
///
/// 接口（公开 H5/Web 接口）：
///   1. 自举 cookie：第一次访问 https://xueqiu.com 取回 xq_a_token
///   2. 搜索：https://xueqiu.com/query/v1/suggest_stock.json?q=keyword
///   3. 报表：https://stock.xueqiu.com/v5/stock/finance/cn/{balance|income|cash_flow}.json
///            ?symbol=SH600000&type=Q4&is_detail=true&count=20
///      - type=Q4 年报；S1 中报；Q1/Q3 季报；all 全部
/// </summary>
public class XueqiuStockSource : IStockDataSource
{
    private readonly HttpClientHandler _handler;
    private readonly HttpClient _http;
    private bool _bootstrapped;
    private DateTime _bootstrappedAt = DateTime.MinValue;
    private static readonly SemaphoreSlim _bootLock = new(1, 1);

    public string Name => "雪球";

    public XueqiuStockSource() : this(null) { }

    internal XueqiuStockSource(HttpMessageHandler? handler)
    {
        _handler = handler as HttpClientHandler ?? new HttpClientHandler
        {
            CookieContainer = new CookieContainer(),
            UseCookies = true,
            AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate,
        };
        _http = handler == null
            ? new HttpClient(_handler) { Timeout = TimeSpan.FromSeconds(20) }
            : new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(20) };
        _http.DefaultRequestHeaders.UserAgent.ParseAdd(
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 ComparetoolWpf/1.0");
        _http.DefaultRequestHeaders.Accept.ParseAdd("application/json, text/plain, */*");
    }

    /// <summary>
    /// 访问雪球若干公开页面以获取 <c>xq_a_token</c> / <c>u</c> 等 cookie。
    /// 由于雪球首页加了 WAF，单纯 GET <c>/</c> 不一定下发 token；
    /// 因此依次尝试若干轻量页面，只要 cookie 容器里出现 <c>xq_a_token</c> 就视为成功。
    /// </summary>
    private async Task EnsureBootstrappedAsync(CancellationToken ct)
    {
        if (_bootstrapped && (DateTime.UtcNow - _bootstrappedAt).TotalMinutes < 30
            && HasToken()) return;
        await _bootLock.WaitAsync(ct);
        try
        {
            if (_bootstrapped && (DateTime.UtcNow - _bootstrappedAt).TotalMinutes < 30
                && HasToken()) return;

            string[] candidates =
            {
                "https://xueqiu.com/hq",
                "https://xueqiu.com/S/SH600000",
                "https://xueqiu.com/",
            };
            foreach (var url in candidates)
            {
                try
                {
                    using var resp = await _http.GetAsync(url, ct);
                    // 不强求 200，只要 cookie 写入即可
                    if (HasToken()) break;
                }
                catch (Exception ex)
                {
                    Logger.Warn($"雪球 bootstrap {url} 失败：{ex.Message}");
                }
            }
            _bootstrapped = true;
            _bootstrappedAt = DateTime.UtcNow;
            if (!HasToken())
                Logger.Warn("雪球 bootstrap 未获取到 xq_a_token cookie，后续请求可能被 WAF 拒绝。");
        }
        finally { _bootLock.Release(); }
    }

    private bool HasToken()
    {
        var cookies = _handler.CookieContainer.GetCookies(new Uri("https://xueqiu.com"));
        foreach (Cookie c in cookies)
        {
            if (c.Name == "xq_a_token" && !string.IsNullOrEmpty(c.Value)) return true;
        }
        return false;
    }

    /// <summary>检查雪球 API 通用错误：<c>{ success:false, error_code, error_description }</c>
    /// 或 <c>{ code: 4xxxxx, success:false }</c>。命中时抛 <see cref="HttpRequestException"/>
    /// 以便上层 Retry/Composite 自动重试或降级。</summary>
    private static void ThrowIfXueqiuError(JObject jo)
    {
        bool? ok = jo.Value<bool?>("success");
        // 旧 API: error_code != 0 也代表失败
        var errCode = jo.Value<string?>("error_code");
        var errDesc = jo.Value<string?>("error_description");
        var code = jo.Value<int?>("code");
        var msg = jo.Value<string?>("message");
        bool failed = ok == false
                      || (errCode != null && errCode != "0")
                      || (code.HasValue && code.Value >= 400000);
        if (failed)
        {
            var detail = errDesc ?? msg ?? errCode ?? code?.ToString() ?? "未知错误";
            throw new HttpRequestException($"雪球接口拒绝请求：{detail}");
        }
    }

    public async Task<List<StockInfo>> SearchStocksAsync(string keyword, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(keyword)) return new List<StockInfo>();
        await EnsureBootstrappedAsync(ct);

        var url = $"https://xueqiu.com/query/v1/suggest_stock.json?q={Uri.EscapeDataString(keyword)}";
        var json = await _http.GetStringAsync(url, ct);
        var jo = JObject.Parse(json);
        ThrowIfXueqiuError(jo);
        var list = new List<StockInfo>();
        if (jo["data"] is not JArray data) return list;

        foreach (var it in data.OfType<JObject>())
        {
            // code 形如 "SH600000" / "SZ000001"
            var code = it.Value<string>("code") ?? "";
            var query = it.Value<string>("query") ?? code;  // 6 位代码
            var name = it.Value<string>("name") ?? "";
            if (code.Length < 8) continue;
            var market = code.Substring(0, 2).ToUpper();   // SH/SZ
            var num = code.Substring(2);
            if (market != "SH" && market != "SZ") continue;
            if (num.Length != 6 || !num.All(char.IsDigit)) continue;
            list.Add(new StockInfo(num, name, market));
        }
        return list;
    }

    public async Task<List<FinancialReport>> GetReportsAsync(
        StockInfo stock, ReportKind kind, ReportPeriodType periodType = ReportPeriodType.All,
        int pageSize = 20, CancellationToken ct = default)
    {
        await EnsureBootstrappedAsync(ct);

        var endpoint = kind switch
        {
            ReportKind.Balance => "balance",
            ReportKind.Income => "income",
            ReportKind.CashFlow => "cash_flow",
            _ => throw new ArgumentOutOfRangeException(nameof(kind)),
        };
        var typeParam = periodType switch
        {
            ReportPeriodType.Annual => "Q4",
            ReportPeriodType.SemiAnnual => "S1",
            ReportPeriodType.Q1 => "Q1",
            ReportPeriodType.Q3 => "Q3",
            _ => "all",
        };
        var url = $"https://stock.xueqiu.com/v5/stock/finance/cn/{endpoint}.json" +
                  $"?symbol={stock.FullCode}&type={typeParam}&is_detail=true&count={pageSize}";
        var json = await _http.GetStringAsync(url, ct);
        var jo = JObject.Parse(json);
        ThrowIfXueqiuError(jo);
        if (jo["data"] is not JObject d) return new List<FinancialReport>();
        if (d["list"] is not JArray rows) return new List<FinancialReport>();

        var fieldMap = kind switch
        {
            ReportKind.Balance => XueqiuBalanceMap,
            ReportKind.Income => XueqiuIncomeMap,
            ReportKind.CashFlow => XueqiuCashFlowMap,
            _ => new Dictionary<string, string>(),
        };

        var result = new List<FinancialReport>();
        foreach (var row in rows.OfType<JObject>())
        {
            // 雪球 report_date 字段是毫秒时间戳
            var ts = row.Value<long?>("report_date");
            if (!ts.HasValue) continue;
            var date = DateTimeOffset.FromUnixTimeMilliseconds(ts.Value).LocalDateTime.Date;
            var report = new FinancialReport
            {
                ReportDate = date,
                PeriodLabel = row.Value<string>("report_name") ?? date.ToString("yyyy-MM-dd"),
                Kind = kind,
                StockFullCode = stock.FullCode,
            };
            foreach (var (xqKey, cnName) in fieldMap)
            {
                report.Items[cnName] = ParseXqValue(row[xqKey]);
            }
            result.Add(report);
        }
        return result.OrderByDescending(r => r.ReportDate).ToList();
    }

    /// <summary>雪球数值字段通常返回 [value, yoy] 数组，取第 0 位。</summary>
    private static double? ParseXqValue(JToken? token)
    {
        if (token == null || token.Type == JTokenType.Null) return null;
        if (token is JArray arr && arr.Count > 0) token = arr[0];
        if (token == null || token.Type == JTokenType.Null) return null;
        if (double.TryParse(token.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var v))
            return v;
        return null;
    }

    // 字段名 → 中文名（与 EastMoneyService 字段表中文名完全一致，便于跨源拼接）
    private static readonly Dictionary<string, string> XueqiuBalanceMap = new()
    {
        ["currency_funds"] = "货币资金",
        ["tradable_fnncl_assets"] = "交易性金融资产",
        ["bill_n_ar"] = "应收票据及应收账款",
        ["accounts_receivable"] = "应收账款",
        ["pre_payment"] = "预付款项",
        ["inventory"] = "存货",
        ["total_current_assets"] = "流动资产合计",
        ["fixed_asset"] = "固定资产",
        ["construction_in_process"] = "在建工程",
        ["intangible_assets"] = "无形资产",
        ["goodwill"] = "商誉",
        ["total_noncurrent_assets"] = "非流动资产合计",
        ["total_assets"] = "资产总计",
        ["st_loan"] = "短期借款",
        ["bill_n_ap"] = "应付票据及应付账款",
        ["contract_liabilities"] = "合同负债",
        ["total_current_liab"] = "流动负债合计",
        ["lt_loan"] = "长期借款",
        ["total_noncurrent_liab"] = "非流动负债合计",
        ["total_liab"] = "负债合计",
        ["share_capital"] = "实收资本(或股本)",
        ["capital_reserve"] = "资本公积",
        ["surplus_reserves"] = "盈余公积",
        ["undstrbtd_profit"] = "未分配利润",
        ["total_quity_atsopc"] = "归属母公司股东权益合计",
        ["total_holders_equity"] = "股东权益合计",
        ["total_liab_n_holders_equity"] = "负债和股东权益总计",
    };

    private static readonly Dictionary<string, string> XueqiuIncomeMap = new()
    {
        ["total_revenue"] = "营业总收入",
        ["revenue"] = "营业收入",
        ["total_op_cost"] = "营业总成本",
        ["op_cost"] = "营业成本",
        ["sales_fee"] = "销售费用",
        ["manage_fee"] = "管理费用",
        ["rd_cost"] = "研发费用",
        ["financing_expenses"] = "财务费用",
        ["op_pft"] = "营业利润",
        ["pft_total"] = "利润总额",
        ["income_tax"] = "所得税",
        ["net_profit"] = "净利润",
        ["np_atsopc"] = "归属母公司股东净利润",
        ["np_atsopc_nrgal_atoopc"] = "扣非净利润",
        ["basic_eps"] = "基本每股收益",
        ["dlt_earnings_per_share"] = "稀释每股收益",
    };

    private static readonly Dictionary<string, string> XueqiuCashFlowMap = new()
    {
        ["goods_sale_and_service_render_cash"] = "销售商品、提供劳务收到的现金",
        ["sub_total_of_ci_from_oa"] = "经营活动现金流入小计",
        ["sub_total_of_cos_from_oa"] = "经营活动现金流出小计",
        ["ncf_from_oa"] = "经营活动产生的现金流量净额",
        ["sub_total_of_ci_from_ia"] = "投资活动现金流入小计",
        ["sub_total_of_cos_from_ia"] = "投资活动现金流出小计",
        ["ncf_from_ia"] = "投资活动产生的现金流量净额",
        ["sub_total_of_ci_from_fa"] = "筹资活动现金流入小计",
        ["sub_total_of_cos_from_fa"] = "筹资活动现金流出小计",
        ["ncf_from_fa"] = "筹资活动产生的现金流量净额",
        ["nicieoce"] = "现金及现金等价物净增加额",
        ["ceace"] = "期末现金及现金等价物余额",
    };
}
