using System.Net.Http;
using System.Text.RegularExpressions;
using ComparetoolWpf.Models;
using Newtonsoft.Json.Linq;

namespace ComparetoolWpf.Services;

/// <summary>
/// 东方财富公开接口数据源。
///
/// 使用到的接口（均为东方财富公开 F10 接口，无需鉴权）：
///  1. 股票联想搜索：
///     https://searchadapter.eastmoney.com/api/suggest/get?input=xxx&type=14
///  2. F10 财务报告期列表：
///     https://emweb.securities.eastmoney.com/PC_HSF10/NewFinanceAnalysis/{zcfzb|lrb|xjllb}DateAjaxNew
///        ?companyType={1银行|2证券|3保险|4一般}&reportDateType=0&code=SH600000
///  3. F10 三大报表数据：
///     https://emweb.securities.eastmoney.com/PC_HSF10/NewFinanceAnalysis/{zcfzb|lrb|xjllb}AjaxNew
///        ?companyType=...&reportDateType=0&reportType=1&dates=2024-12-31,2024-09-30&code=SH600000
///
/// 注：东方财富接口字段及命名可能随时调整。本类只做轻量适配，遇到接口变化时
/// 只需修改 <see cref="ParseF10Reports"/> 与字段映射字典。
/// </summary>
public class EastMoneyService : IStockDataSource
{
    private readonly HttpClient _http;

    public string Name => "东方财富";

    public EastMoneyService()
    {
        _http = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(20),
        };
        // 东方财富部分接口对 UA 有简单校验
        _http.DefaultRequestHeaders.UserAgent.ParseAdd(
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 ComparetoolWpf/1.0");
        _http.DefaultRequestHeaders.Referrer = new Uri("https://emweb.securities.eastmoney.com/");
    }

    #region 股票搜索

    /// <summary>
    /// 根据关键字（代码或名称片段）搜索 A 股。
    /// </summary>
    public async Task<List<StockInfo>> SearchStocksAsync(string keyword, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(keyword)) return new List<StockInfo>();

        // type=14 表示 A 股
        var url = $"https://searchadapter.eastmoney.com/api/suggest/get" +
                  $"?input={Uri.EscapeDataString(keyword)}&type=14&token=D43BF722C8E33BDC906FB84D85E326E8&count=20";
        var json = await _http.GetStringAsync(url, ct);
        var jo = JObject.Parse(json);
        var list = new List<StockInfo>();
        // 安全访问：QuotationCodeTable 可能为 null/JValue
        JArray? arr = null;
        if (jo["QuotationCodeTable"] is JObject qct)
        {
            arr = qct["Data"] as JArray;
        }
        if (arr == null) return list;

        foreach (var item in arr)
        {
            var code = item.Value<string>("Code") ?? "";
            var name = item.Value<string>("Name") ?? "";
            // MktNum: 1=SH, 0=SZ, 105/106... = 美股, 116=港股 ... 仅取 0/1
            var mkt = item.Value<string>("MktNum");
            string market = mkt switch
            {
                "1" => "SH",
                "0" => "SZ",
                _ => string.Empty,
            };
            if (string.IsNullOrEmpty(market)) continue;
            if (!Regex.IsMatch(code, @"^\d{6}$")) continue;
            list.Add(new StockInfo(code, name, market));
        }
        return list;
    }

    #endregion

    #region 三大报表

    /// <summary>
    /// 拉取指定股票指定报表的多期历史（按报告期倒序）。
    /// 数据源：东方财富 F10 新版财务分析接口。
    /// </summary>
    /// <param name="stock">股票。</param>
    /// <param name="kind">报表类型。</param>
    /// <param name="periodType">报告期过滤。</param>
    /// <param name="pageSize">最大期数。</param>
    public async Task<List<FinancialReport>> GetReportsAsync(
        StockInfo stock,
        ReportKind kind,
        ReportPeriodType periodType = ReportPeriodType.All,
        int pageSize = 20,
        CancellationToken ct = default)
    {
        // 不同报表的 ajax 名（页面 / 数据），名字以 New 结尾
        (string dateApi, string dataApi) = kind switch
        {
            ReportKind.Balance => ("zcfzbDateAjaxNew", "zcfzbAjaxNew"),
            ReportKind.Income => ("lrbDateAjaxNew", "lrbAjaxNew"),
            ReportKind.CashFlow => ("xjllbDateAjaxNew", "xjllbAjaxNew"),
            _ => throw new ArgumentOutOfRangeException(nameof(kind)),
        };

        // 探测公司类型（4=一般工商, 1=银行, 2=证券, 3=保险）
        // 依次尝试，第一个成功返回非空报告期列表的 companyType 即视为正确类型。
        int[] companyTypeCandidates = { 4, 1, 2, 3 };

        Exception? lastError = null;
        foreach (var ct1 in companyTypeCandidates)
        {
            try
            {
                var dates = await GetReportDatesAsync(stock, dateApi, ct1, periodType, pageSize, ct);
                if (dates.Count == 0) continue;

                var rows = await GetReportRowsAsync(stock, dataApi, ct1, dates, ct);
                if (rows.Count == 0) continue;

                return ParseF10Reports(rows, kind, stock);
            }
            catch (Exception ex)
            {
                lastError = ex;
            }
        }

        throw new InvalidOperationException(
            $"未获取到 {stock.Name}({stock.FullCode}) 的 {kind} 数据。" +
            (lastError != null ? $" 最后错误：{lastError.Message}" : string.Empty));
    }

    /// <summary>
    /// 获取报告期列表（zcfzbDateAjaxNew 等）。返回筛选后的 (报告期, 类型标签) 列表。
    /// </summary>
    private async Task<List<(string Date, string TypeLabel)>> GetReportDatesAsync(
        StockInfo stock, string dateApi, int companyType,
        ReportPeriodType periodType, int max, CancellationToken ct)
    {
        var url = $"https://emweb.securities.eastmoney.com/PC_HSF10/NewFinanceAnalysis/{dateApi}" +
                  $"?companyType={companyType}&reportDateType=0&code={stock.FullCode}";
        var json = await _http.GetStringAsync(url, ct);

        // 接口可能直接返回数组，也可能返回 { data: [...] }
        var token = JToken.Parse(json);
        JArray? arr = token as JArray;
        if (arr == null && token is JObject obj)
        {
            arr = obj["data"] as JArray;
        }

        var result = new List<(string, string)>();
        if (arr == null) return result;

        foreach (var it in arr.OfType<JObject>())
        {
            var date = it.Value<string>("REPORT_DATE") ?? string.Empty;
            var label = it.Value<string>("DATATYPE") ?? string.Empty;
            if (string.IsNullOrEmpty(date)) continue;
            // 截取 yyyy-MM-dd
            if (date.Length >= 10) date = date.Substring(0, 10);

            if (!MatchPeriodType(date, label, periodType)) continue;

            result.Add((date, label));
            if (result.Count >= max) break;
        }
        return result;
    }

    /// <summary>判断给定报告期/标签是否符合用户选择的报告期类型。</summary>
    private static bool MatchPeriodType(string date, string label, ReportPeriodType pt)
    {
        if (pt == ReportPeriodType.All) return true;
        // 优先用 DATATYPE 文案
        if (!string.IsNullOrEmpty(label))
        {
            return pt switch
            {
                ReportPeriodType.Annual => label.Contains("年报"),
                ReportPeriodType.SemiAnnual => label.Contains("中报") || label.Contains("半年"),
                ReportPeriodType.Q1 => label.Contains("一季"),
                ReportPeriodType.Q3 => label.Contains("三季"),
                _ => true,
            };
        }
        // 退而求其次：按月日判断
        if (date.Length >= 10 && DateTime.TryParse(date, out var d))
        {
            return pt switch
            {
                ReportPeriodType.Annual => d.Month == 12 && d.Day == 31,
                ReportPeriodType.SemiAnnual => d.Month == 6 && d.Day == 30,
                ReportPeriodType.Q1 => d.Month == 3 && d.Day == 31,
                ReportPeriodType.Q3 => d.Month == 9 && d.Day == 30,
                _ => true,
            };
        }
        return true;
    }

    /// <summary>按 dates 参数拉取数据行。dates 为逗号分隔的 yyyy-MM-dd。</summary>
    private async Task<JArray> GetReportRowsAsync(
        StockInfo stock, string dataApi, int companyType,
        List<(string Date, string TypeLabel)> dates, CancellationToken ct)
    {
        // 一次最多 5 期，超过分批
        var combined = new JArray();
        const int BatchSize = 5;
        for (int i = 0; i < dates.Count; i += BatchSize)
        {
            var batch = dates.Skip(i).Take(BatchSize).Select(d => d.Date);
            string datesParam = string.Join(",", batch);

            var url = $"https://emweb.securities.eastmoney.com/PC_HSF10/NewFinanceAnalysis/{dataApi}" +
                      $"?companyType={companyType}&reportDateType=0&reportType=1" +
                      $"&dates={Uri.EscapeDataString(datesParam)}&code={stock.FullCode}";
            var json = await _http.GetStringAsync(url, ct);

            var token = JToken.Parse(json);
            JArray? arr = token as JArray;
            if (arr == null && token is JObject obj)
            {
                arr = obj["data"] as JArray;
            }
            if (arr == null) continue;
            foreach (var item in arr) combined.Add(item);
        }
        return combined;
    }

    /// <summary>解析 F10 行集合为 <see cref="FinancialReport"/>。</summary>
    private static List<FinancialReport> ParseF10Reports(JArray data, ReportKind kind, StockInfo stock)
    {
        var map = kind switch
        {
            ReportKind.Balance => BalanceFieldMap,
            ReportKind.Income => IncomeFieldMap,
            ReportKind.CashFlow => CashFlowFieldMap,
            _ => new Dictionary<string, string>(),
        };

        var list = new List<FinancialReport>();
        foreach (var row in data.OfType<JObject>())
        {
            var dateStr = row.Value<string>("REPORT_DATE");
            if (string.IsNullOrEmpty(dateStr)) continue;
            if (!DateTime.TryParse(dateStr, out var date)) continue;

            var report = new FinancialReport
            {
                ReportDate = date,
                // 优先组合 "2024年报"/"2024中报"，否则回退按日期推断
                PeriodLabel = BuildPeriodLabelFromRow(row, date),
                Kind = kind,
                StockFullCode = stock.FullCode,
            };

            foreach (var (apiField, cnName) in map)
            {
                var token = row[apiField];
                if (token == null || token.Type == JTokenType.Null)
                {
                    report.Items[cnName] = null;
                    continue;
                }
                if (double.TryParse(token.ToString(),
                        System.Globalization.NumberStyles.Any,
                        System.Globalization.CultureInfo.InvariantCulture,
                        out var v))
                    report.Items[cnName] = v;
                else
                    report.Items[cnName] = null;
            }

            list.Add(report);
        }
        // 报告期倒序
        return list.OrderByDescending(r => r.ReportDate).ToList();
    }

    /// <summary>根据报告期日期生成 "2024年报" / "2024中报" 等标签。</summary>
    private static string BuildPeriodLabel(DateTime date)
    {
        return (date.Month, date.Day) switch
        {
            (12, 31) => $"{date.Year}年报",
            (6, 30) => $"{date.Year}中报",
            (3, 31) => $"{date.Year}一季报",
            (9, 30) => $"{date.Year}三季报",
            _ => date.ToString("yyyy-MM-dd"),
        };
    }

    /// <summary>结合行内 DATATYPE 与日期，构造更友好的报告期标签。</summary>
    private static string BuildPeriodLabelFromRow(JObject row, DateTime date)
    {
        var dt = row.Value<string>("DATATYPE");
        if (!string.IsNullOrWhiteSpace(dt))
        {
            // 如果 DATATYPE 已经是 "2024年报" 直接返回；否则前缀年份
            if (dt.Contains(date.Year.ToString())) return dt;
            return $"{date.Year}{dt}";
        }
        return BuildPeriodLabel(date);
    }

    #endregion

    #region 字段映射表

    /// <summary>资产负债表常用字段映射。</summary>
    public static readonly Dictionary<string, string> BalanceFieldMap = new()
    {
        ["MONETARYFUNDS"] = "货币资金",
        ["TRADE_FINASSET"] = "交易性金融资产",
        ["NOTE_ACCOUNTS_RECE"] = "应收票据及应收账款",
        ["ACCOUNTS_RECE"] = "应收账款",
        ["PREPAYMENT"] = "预付款项",
        ["INVENTORY"] = "存货",
        ["TOTAL_CURRENT_ASSETS"] = "流动资产合计",
        ["FIXED_ASSET"] = "固定资产",
        ["CIP"] = "在建工程",
        ["INTANGIBLE_ASSET"] = "无形资产",
        ["GOODWILL"] = "商誉",
        ["TOTAL_NONCURRENT_ASSETS"] = "非流动资产合计",
        ["TOTAL_ASSETS"] = "资产总计",
        ["SHORT_LOAN"] = "短期借款",
        ["NOTE_ACCOUNTS_PAYABLE"] = "应付票据及应付账款",
        ["CONTRACT_LIAB"] = "合同负债",
        ["TOTAL_CURRENT_LIAB"] = "流动负债合计",
        ["LONG_LOAN"] = "长期借款",
        ["TOTAL_NONCURRENT_LIAB"] = "非流动负债合计",
        ["TOTAL_LIABILITIES"] = "负债合计",
        ["SHARE_CAPITAL"] = "实收资本(或股本)",
        ["CAPITAL_RESERVE"] = "资本公积",
        ["SURPLUS_RESERVE"] = "盈余公积",
        ["UNASSIGN_RPOFIT"] = "未分配利润",
        ["TOTAL_PARENT_EQUITY"] = "归属母公司股东权益合计",
        ["TOTAL_EQUITY"] = "股东权益合计",
        ["TOTAL_LIAB_EQUITY"] = "负债和股东权益总计",
    };

    /// <summary>利润表常用字段映射。</summary>
    public static readonly Dictionary<string, string> IncomeFieldMap = new()
    {
        ["TOTAL_OPERATE_INCOME"] = "营业总收入",
        ["OPERATE_INCOME"] = "营业收入",
        ["TOTAL_OPERATE_COST"] = "营业总成本",
        ["OPERATE_COST"] = "营业成本",
        ["SALE_EXPENSE"] = "销售费用",
        ["MANAGE_EXPENSE"] = "管理费用",
        ["RESEARCH_EXPENSE"] = "研发费用",
        ["FINANCE_EXPENSE"] = "财务费用",
        ["OPERATE_PROFIT"] = "营业利润",
        ["TOTAL_PROFIT"] = "利润总额",
        ["INCOME_TAX"] = "所得税",
        ["NETPROFIT"] = "净利润",
        ["PARENT_NETPROFIT"] = "归属母公司股东净利润",
        ["DEDUCT_PARENT_NETPROFIT"] = "扣非净利润",
        ["BASIC_EPS"] = "基本每股收益",
        ["DILUTED_EPS"] = "稀释每股收益",
    };

    /// <summary>现金流量表常用字段映射。</summary>
    public static readonly Dictionary<string, string> CashFlowFieldMap = new()
    {
        ["SALES_SERVICES"] = "销售商品、提供劳务收到的现金",
        ["TOTAL_OPERATE_INFLOW"] = "经营活动现金流入小计",
        ["TOTAL_OPERATE_OUTFLOW"] = "经营活动现金流出小计",
        ["NETCASH_OPERATE"] = "经营活动产生的现金流量净额",
        ["TOTAL_INVEST_INFLOW"] = "投资活动现金流入小计",
        ["TOTAL_INVEST_OUTFLOW"] = "投资活动现金流出小计",
        ["NETCASH_INVEST"] = "投资活动产生的现金流量净额",
        ["TOTAL_FINANCE_INFLOW"] = "筹资活动现金流入小计",
        ["TOTAL_FINANCE_OUTFLOW"] = "筹资活动现金流出小计",
        ["NETCASH_FINANCE"] = "筹资活动产生的现金流量净额",
        ["CCE_ADD"] = "现金及现金等价物净增加额",
        ["END_CCE"] = "期末现金及现金等价物余额",
    };

    #endregion
}
