namespace StockAnalyzer.DataSource.Eastmoney;

/// <summary>东方财富公开行情接口的地址与筛选条件常量。</summary>
internal static class EastmoneyEndpoints
{
    public const string QuoteHost = "https://push2.eastmoney.com";
    public const string HistoryHost = "https://push2his.eastmoney.com";
    public const string DataCenterHost = "https://datacenter-web.eastmoney.com";
    public const string SearchHost = "https://searchapi.eastmoney.com";

    /// <summary>行情列表（分页拉取全市场代码）。</summary>
    public const string ClistPath = "/api/qt/clist/get";

    /// <summary>搜索建议（代码 / 名称 / 拼音首字母模糊匹配）。</summary>
    public const string SuggestPath = "/api/suggest/get";

    /// <summary>搜索建议接口的公开 token（东财前端硬编码，非个人凭证）。</summary>
    public const string SuggestToken = "D43BF722C8E33BDC906FB84D85E326E8";

    /// <summary>批量快照（支持多个 secid，fltt=2 时直接返回浮点数）。</summary>
    public const string UlistPath = "/api/qt/ulist.np/get";

    /// <summary>历史 K 线。</summary>
    public const string KlinePath = "/api/qt/stock/kline/get";

    /// <summary>数据中心通用查询（财报）。</summary>
    public const string DataCenterPath = "/api/data/v1/get";

    /// <summary>A 股：深主板 + 创业板 + 沪主板 + 科创板 + 北交所。</summary>
    public const string FilterAShare = "m:0+t:6,m:0+t:80,m:1+t:2,m:1+t:23,m:0+t:81+s:2048";

    /// <summary>港股：主板 + 创业板 + 蓝筹等。</summary>
    public const string FilterHongKong = "m:128+t:3,m:128+t:4,m:128+t:1,m:128+t:2";

    /// <summary>列表接口所需字段：代码 / 市场 / 名称。</summary>
    public const string ListFields = "f12,f13,f14";

    /// <summary>快照字段集合。f9=PE(动态)，f114=PE(静态)，f115=PE(TTM)，f23=PB。</summary>
    public const string QuoteFields =
        "f2,f3,f4,f5,f6,f7,f8,f9,f10,f12,f13,f14,f15,f16,f17,f18,f20,f21,f23,f114,f115";

    public const string KlineFields1 = "f1,f2,f3,f4,f5,f6";

    public const string KlineFields2 = "f51,f52,f53,f54,f55,f56,f57,f58,f59,f60,f61";

    /// <summary>业绩报表数据集（含 BASIC_EPS / BPS / NOTICE_DATE）。</summary>
    public const string FinanceReportName = "RPT_LICO_FN_CPD";

    public const string FinanceColumns =
        "SECURITY_CODE,SECURITY_NAME_ABBR,REPORTDATE,NOTICE_DATE,BASIC_EPS,DEDUCT_BASIC_EPS," +
        "TOTAL_OPERATE_INCOME,PARENT_NETPROFIT,WEIGHTAVG_ROE,BPS";

    /// <summary>港股主要财务指标数据集（直接提供 EPS_TTM 与 BPS）。</summary>
    public const string HongKongFinanceReportName = "RPT_HKF10_FN_MAININDICATOR";

    public const string HongKongFinanceColumns =
        "SECUCODE,SECURITY_CODE,REPORT_DATE,DATE_TYPE_CODE,BASIC_EPS,EPS_TTM,BPS," +
        "HOLDER_PROFIT,ROE_AVG,CURRENCY";

    /// <summary>港股定期报告的典型披露滞后天数（接口未提供公告日）。</summary>
    public const int HongKongNoticeLagDays = 60;
}
