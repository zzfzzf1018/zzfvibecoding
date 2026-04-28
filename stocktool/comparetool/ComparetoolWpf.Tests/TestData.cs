using ComparetoolWpf.Models;

namespace ComparetoolWpf.Tests;

/// <summary>测试用工厂：构造各种典型 <see cref="FinancialReport"/>。</summary>
internal static class TestData
{
    public static FinancialReport Balance(DateTime date, double currentAssets, double currentLiab,
        double totalAssets, double equity, double ar = 0, double inv = 0, double ap = 0,
        string code = "SH600000")
        => new()
        {
            ReportDate = date,
            PeriodLabel = $"{date.Year}-{date.Month:D2}",
            Kind = ReportKind.Balance,
            StockFullCode = code,
            Items = new()
            {
                ["流动资产合计"] = currentAssets,
                ["流动负债合计"] = currentLiab,
                ["资产总计"] = totalAssets,
                ["归属母公司股东权益合计"] = equity,
                ["应收账款"] = ar,
                ["存货"] = inv,
                ["应付票据及应付账款"] = ap,
            },
        };

    public static FinancialReport Income(DateTime date, double revenue, double netProfit,
        double cost = 0, string code = "SH600000")
        => new()
        {
            ReportDate = date,
            PeriodLabel = $"{date.Year}-{date.Month:D2}",
            Kind = ReportKind.Income,
            StockFullCode = code,
            Items = new()
            {
                ["营业总收入"] = revenue,
                ["营业成本"] = cost,
                ["归属母公司股东净利润"] = netProfit,
            },
        };

    public static FinancialReport CashFlow(DateTime date, double ocf, double investOutflow,
        string code = "SH600000")
        => new()
        {
            ReportDate = date,
            PeriodLabel = $"{date.Year}-{date.Month:D2}",
            Kind = ReportKind.CashFlow,
            StockFullCode = code,
            Items = new()
            {
                ["经营活动产生的现金流量净额"] = ocf,
                ["投资活动现金流出小计"] = investOutflow,
                ["经营活动现金流入小计"] = ocf + 100,
            },
        };
}
