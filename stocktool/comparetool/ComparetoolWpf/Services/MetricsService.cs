using ComparetoolWpf.Models;

namespace ComparetoolWpf.Services;

/// <summary>
/// 财务指标计算：同比、环比、ROE 杜邦拆解。
/// 输入：同一只股票同一报表种类的多期 <see cref="FinancialReport"/> 列表。
/// </summary>
public static class MetricsService
{
    /// <summary>
    /// 计算指定指标的同比 / 环比序列。
    /// 同比：与上一年度同月同日的报告期对比；
    /// 环比：与上一相邻报告期对比（按列表中的相邻关系）。
    /// </summary>
    /// <param name="reports">报表（任意顺序，函数内部会按报告期升序处理）。</param>
    /// <param name="itemName">指标中文名（必须存在于 <see cref="FinancialReport.Items"/>）。</param>
    public static List<GrowthRow> ComputeGrowth(IEnumerable<FinancialReport> reports, string itemName)
    {
        var ordered = reports.OrderBy(r => r.ReportDate).ToList();
        var result = new List<GrowthRow>();
        foreach (var r in ordered)
        {
            r.Items.TryGetValue(itemName, out var v);
            var row = new GrowthRow
            {
                ReportDate = r.ReportDate,
                PeriodLabel = r.PeriodLabel,
                Value = v,
            };

            // 环比：上一相邻期
            var prev = ordered.LastOrDefault(x => x.ReportDate < r.ReportDate);
            if (prev != null && prev.Items.TryGetValue(itemName, out var pv))
            {
                row.QoQBase = pv;
                row.QoQ = Ratio(v, pv);
            }

            // 同比：相同月日、年份-1
            var yoyDate = r.ReportDate.AddYears(-1);
            var yoy = ordered.FirstOrDefault(x => x.ReportDate == yoyDate);
            if (yoy != null && yoy.Items.TryGetValue(itemName, out var yv))
            {
                row.YoYBase = yv;
                row.YoY = Ratio(v, yv);
            }

            result.Add(row);
        }
        // 按时间倒序展示
        result.Reverse();
        return result;
    }

    /// <summary>
    /// 杜邦分析。需要传入资产负债表 + 利润表。
    /// 取每个报告期资产负债表与同期利润表配对计算。
    /// </summary>
    public static List<DuPontRow> ComputeDuPont(
        IEnumerable<FinancialReport> balances,
        IEnumerable<FinancialReport> incomes)
    {
        var balDict = balances.ToDictionary(b => b.ReportDate, b => b);
        var incDict = incomes.ToDictionary(i => i.ReportDate, i => i);

        var result = new List<DuPontRow>();
        foreach (var date in incDict.Keys.Intersect(balDict.Keys).OrderByDescending(d => d))
        {
            var inc = incDict[date];
            var bal = balDict[date];

            inc.Items.TryGetValue("归属母公司股东净利润", out var np);
            inc.Items.TryGetValue("营业总收入", out var rev);
            bal.Items.TryGetValue("资产总计", out var ta);
            bal.Items.TryGetValue("归属母公司股东权益合计", out var eq);

            var row = new DuPontRow
            {
                ReportDate = date,
                PeriodLabel = inc.PeriodLabel,
                NetProfit = np,
                Revenue = rev,
                TotalAssets = ta,
                Equity = eq,
                NetMargin = SafeDiv(np, rev),
                AssetTurnover = SafeDiv(rev, ta),
                EquityMultiplier = SafeDiv(ta, eq),
            };
            // ROE = NM * AT * EM
            if (row.NetMargin.HasValue && row.AssetTurnover.HasValue && row.EquityMultiplier.HasValue)
                row.Roe = row.NetMargin * row.AssetTurnover * row.EquityMultiplier;
            else
                row.Roe = SafeDiv(np, eq); // 退化为直接 ROE

            result.Add(row);
        }
        return result;
    }

    private static double? Ratio(double? v, double? baseVal)
    {
        if (!v.HasValue || !baseVal.HasValue) return null;
        if (Math.Abs(baseVal.Value) < 1e-9) return null;
        return (v.Value - baseVal.Value) / Math.Abs(baseVal.Value);
    }

    private static double? SafeDiv(double? a, double? b)
    {
        if (!a.HasValue || !b.HasValue) return null;
        if (Math.Abs(b.Value) < 1e-9) return null;
        return a.Value / b.Value;
    }
}
