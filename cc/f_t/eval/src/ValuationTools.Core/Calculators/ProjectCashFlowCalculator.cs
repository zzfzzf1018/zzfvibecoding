using ValuationTools.Core.Common;

namespace ValuationTools.Core.Calculators;

public sealed class ProjectCashFlowInput
{
    /// <summary>t=0 时点的初始投资（正数表示流出）。</summary>
    public double InitialInvestment { get; init; }
    /// <summary>第 1 年起的各年现金流。</summary>
    public IReadOnlyList<double> CashFlows { get; init; } = Array.Empty<double>();
    public double DiscountRate { get; init; }
    /// <summary>再投资收益率，用于 MIRR；不填则使用折现率。</summary>
    public double ReinvestmentRate { get; init; }
}

public sealed record ProjectYearRow(int Year, double CashFlow, double DiscountFactor, double PresentValue, double CumulativePresentValue);

public sealed class ProjectCashFlowResult
{
    public IReadOnlyList<ProjectYearRow> Years { get; init; } = Array.Empty<ProjectYearRow>();
    public double Npv { get; init; }
    public double? Irr { get; init; }
    public double? Mirr { get; init; }
    /// <summary>获利指数 = 未来现金流现值 / 初始投资。</summary>
    public double ProfitabilityIndex { get; init; }
    public double? PaybackPeriod { get; init; }
    public double? DiscountedPaybackPeriod { get; init; }
    public double TotalPresentValue { get; init; }
    public string Judgement { get; init; } = string.Empty;
}

/// <summary>项目现金流评估：NPV、IRR、MIRR、获利指数与回收期。</summary>
public static class ProjectCashFlowCalculator
{
    public static ProjectCashFlowResult Calculate(ProjectCashFlowInput input)
    {
        var flows = new List<double> { -Math.Abs(input.InitialInvestment) };
        flows.AddRange(input.CashFlows);
        if (flows.Count < 2)
            throw new ArgumentException("请至少填写一年的未来现金流。");

        var rows = new List<ProjectYearRow>();
        double cumulative = 0, pvFuture = 0;
        for (int year = 1; year < flows.Count; year++)
        {
            double df = FinancialMath.DiscountFactor(input.DiscountRate, year);
            double pv = flows[year] * df;
            pvFuture += pv;
            cumulative += pv;
            rows.Add(new ProjectYearRow(year, flows[year], df, pv, cumulative));
        }

        double npv = flows[0] + pvFuture;
        double? irr = FinancialMath.Irr(flows);
        double initial = Math.Abs(input.InitialInvestment);

        var discountedFlows = new List<double> { flows[0] };
        discountedFlows.AddRange(rows.Select(r => r.PresentValue));

        return new ProjectCashFlowResult
        {
            Years = rows,
            Npv = npv,
            Irr = irr,
            Mirr = CalculateMirr(flows, input.DiscountRate, input.ReinvestmentRate > 0 ? input.ReinvestmentRate : input.DiscountRate),
            ProfitabilityIndex = initial > 0 ? pvFuture / initial : 0,
            PaybackPeriod = FinancialMath.PaybackPeriod(flows),
            DiscountedPaybackPeriod = FinancialMath.PaybackPeriod(discountedFlows),
            TotalPresentValue = pvFuture,
            Judgement = npv > 0
                ? "NPV 为正，按给定折现率该项目创造价值"
                : "NPV 为负，按给定折现率该项目毁灭价值"
        };
    }

    private static double? CalculateMirr(IReadOnlyList<double> flows, double financeRate, double reinvestRate)
    {
        int n = flows.Count - 1;
        if (n <= 0) return null;

        double pvNegative = 0, fvPositive = 0;
        for (int i = 0; i < flows.Count; i++)
        {
            if (flows[i] < 0) pvNegative += flows[i] * FinancialMath.DiscountFactor(financeRate, i);
            else fvPositive += flows[i] * Math.Pow(1 + reinvestRate, n - i);
        }
        if (pvNegative >= 0 || fvPositive <= 0) return null;
        return Math.Pow(fvPositive / -pvNegative, 1.0 / n) - 1.0;
    }
}
