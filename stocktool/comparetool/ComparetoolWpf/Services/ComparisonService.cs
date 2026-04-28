using ComparetoolWpf.Models;

namespace ComparetoolWpf.Services;

/// <summary>
/// 财务报表对比与分析服务（纯逻辑，无 UI 依赖）。
/// </summary>
public static class ComparisonService
{
    /// <summary>
    /// 同一只股票两期报表对比。
    /// </summary>
    /// <param name="baseReport">基准期报表（一般是较早期或上一年同期）。</param>
    /// <param name="compareReport">对比期报表（一般是最新期）。</param>
    /// <param name="highlightThreshold">变化幅度阈值（如 0.2 表示 ±20%），超过则在结果中 IsHighlighted=true。</param>
    public static List<ComparisonRow> ComparePeriods(
        FinancialReport baseReport,
        FinancialReport compareReport,
        double highlightThreshold = 0.2)
    {
        if (baseReport.Kind != compareReport.Kind)
            throw new ArgumentException("两个报表的种类必须一致。");

        // 取并集，保持基准期顺序
        var keys = baseReport.Items.Keys
            .Concat(compareReport.Items.Keys.Where(k => !baseReport.Items.ContainsKey(k)))
            .ToList();

        var rows = new List<ComparisonRow>();
        foreach (var key in keys)
        {
            baseReport.Items.TryGetValue(key, out var bv);
            compareReport.Items.TryGetValue(key, out var cv);
            var row = new ComparisonRow
            {
                Item = key,
                BaseValue = bv,
                CompareValue = cv,
            };
            if (row.ChangeRatio.HasValue && Math.Abs(row.ChangeRatio.Value) >= highlightThreshold)
            {
                row.IsHighlighted = true;
            }
            rows.Add(row);
        }
        return rows;
    }

    /// <summary>
    /// 多只股票同一报表的“百分比报表”横向对比。
    /// 把每只股票各指标除以该公司的“基准项”（资产总计 / 营业总收入 / 经营活动现金流入小计），
    /// 形成可比的同口径百分比。
    /// </summary>
    /// <param name="reports">每只股票一份同种报表（同一报告期最佳）。</param>
    /// <param name="kind">报表类型（决定基准项）。</param>
    public static List<MultiStockRow> CommonSizeCompare(
        IReadOnlyList<FinancialReport> reports,
        ReportKind kind)
    {
        if (reports.Count == 0) return new List<MultiStockRow>();
        if (reports.Any(r => r.Kind != kind))
            throw new ArgumentException("所有报表种类必须与 kind 一致。");

        // 不同报表选择不同的基准项
        string baseItem = kind switch
        {
            ReportKind.Balance => "资产总计",
            ReportKind.Income => "营业总收入",
            ReportKind.CashFlow => "经营活动现金流入小计",
            _ => throw new ArgumentOutOfRangeException(nameof(kind)),
        };

        // 收集所有指标（保持第一个报表的顺序，再追加其他独有项）
        var allItems = new List<string>();
        var seen = new HashSet<string>();
        foreach (var rep in reports)
        {
            foreach (var k in rep.Items.Keys)
            {
                if (seen.Add(k)) allItems.Add(k);
            }
        }

        var rows = new List<MultiStockRow>();
        foreach (var item in allItems)
        {
            var row = new MultiStockRow { Item = item };
            foreach (var rep in reports)
            {
                rep.Items.TryGetValue(item, out var v);
                rep.Items.TryGetValue(baseItem, out var bv);
                row.RawValues[rep.StockFullCode] = v;
                if (v.HasValue && bv.HasValue && Math.Abs(bv.Value) > 1e-9)
                    row.Percentages[rep.StockFullCode] = v.Value / bv.Value;
                else
                    row.Percentages[rep.StockFullCode] = null;
            }
            rows.Add(row);
        }
        return rows;
    }
}
