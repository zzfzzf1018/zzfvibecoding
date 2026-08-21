using ValuationTools.Core.Common;

namespace ValuationTools.Core.Calculators;

public sealed class GrowthInput
{
    public double BeginningValue { get; init; }
    public double EndingValue { get; init; }
    public double Years { get; init; }

    public double ReturnOnEquity { get; init; }
    public double PayoutRatio { get; init; }

    /// <summary>再投资率（留存用于扩张的比例）。</summary>
    public double ReinvestmentRate { get; init; }
    public double ReturnOnInvestedCapital { get; init; }

    public double PeRatio { get; init; }
    public double DiscountRate { get; init; }
}

public sealed class GrowthResult
{
    public double? Cagr { get; init; }
    /// <summary>可持续增长率 g = ROE × 留存率。</summary>
    public double SustainableGrowthRate { get; init; }
    /// <summary>基本面增长率 g = 再投资率 × ROIC。</summary>
    public double FundamentalGrowthRate { get; init; }
    /// <summary>当前 PE 隐含的永续增长率 g = r − 分红率 / PE。</summary>
    public double? PeImpliedGrowthRate { get; init; }
    /// <summary>按 CAGR 计算的翻倍所需年数。</summary>
    public double? YearsToDouble { get; init; }
    /// <summary>72 法则估算的翻倍年数。</summary>
    public double? RuleOf72Years { get; init; }
    public double TotalGrowthMultiple { get; init; }
    public string? Warning { get; init; }
}

/// <summary>增长率工具：历史 CAGR、可持续增长率、基本面增长率与市场隐含增长率。</summary>
public static class GrowthCalculator
{
    public static GrowthResult Calculate(GrowthInput input)
    {
        double? cagr = FinancialMath.Cagr(input.BeginningValue, input.EndingValue, input.Years);
        double sustainable = input.ReturnOnEquity * (1 - input.PayoutRatio);
        double fundamental = input.ReinvestmentRate * input.ReturnOnInvestedCapital;

        double? peImplied = input.PeRatio > 0 && input.PayoutRatio > 0
            ? input.DiscountRate - input.PayoutRatio / input.PeRatio
            : null;

        double? yearsToDouble = cagr is > 0 ? Math.Log(2) / Math.Log(1 + cagr.Value) : null;
        double? ruleOf72 = cagr is > 0 ? 72.0 / (cagr.Value * 100.0) : null;

        string? warning = null;
        if (cagr is null)
            warning = "需要正的期初值、期末值与年数才能计算 CAGR。";
        else if (cagr is > 0.3)
            warning = "历史 CAGR 超过 30%，直接外推到未来通常过于乐观，建议做增长衰减假设。";

        return new GrowthResult
        {
            Cagr = cagr,
            SustainableGrowthRate = sustainable,
            FundamentalGrowthRate = fundamental,
            PeImpliedGrowthRate = peImplied,
            YearsToDouble = yearsToDouble,
            RuleOf72Years = ruleOf72,
            TotalGrowthMultiple = input.BeginningValue > 0 ? input.EndingValue / input.BeginningValue : 0,
            Warning = warning
        };
    }
}
