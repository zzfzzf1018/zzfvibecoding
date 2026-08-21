using ValuationTools.Core.Common;

namespace ValuationTools.Core.Calculators;

public sealed class DdmInput
{
    /// <summary>最近一期每股股利 D0。</summary>
    public double CurrentDividend { get; init; }
    /// <summary>高速增长期年数，0 表示直接使用戈登单阶段模型。</summary>
    public int HighGrowthYears { get; init; }
    public double HighGrowthRate { get; init; }
    /// <summary>永续增长率。</summary>
    public double StableGrowthRate { get; init; }
    /// <summary>股权成本（折现率）。</summary>
    public double CostOfEquity { get; init; }
    /// <summary>是否在高增长与稳定增长之间线性过渡（H 模型式的渐变）。</summary>
    public bool LinearFade { get; init; }
    public double CurrentPrice { get; init; }
}

public sealed record DdmYearRow(int Year, double GrowthRate, double Dividend, double DiscountFactor, double PresentValue);

public sealed class DdmResult
{
    public IReadOnlyList<DdmYearRow> Years { get; init; } = Array.Empty<DdmYearRow>();
    public double PresentValueOfDividends { get; init; }
    public double TerminalValue { get; init; }
    public double PresentValueOfTerminal { get; init; }
    public double IntrinsicValue { get; init; }
    public double DividendYieldOnCost { get; init; }
    public double? UpsidePercent { get; init; }
    /// <summary>按当前股价反推的隐含股权成本（内部收益率）。</summary>
    public double? ImpliedReturn { get; init; }
    public string? Warning { get; init; }
}

/// <summary>股利折现模型（DDM），支持单阶段戈登模型与两阶段/渐变模型。</summary>
public static class DdmCalculator
{
    public static DdmResult Calculate(DdmInput input)
    {
        if (input.CostOfEquity - input.StableGrowthRate <= 1e-6)
            throw new InvalidOperationException("股权成本必须大于永续增长率。");
        if (input.CurrentDividend < 0)
            throw new ArgumentException("股利不能为负数。");
        if (input.HighGrowthYears < 0 || input.HighGrowthYears > 100)
            throw new ArgumentException("高增长期年数应在 0~100 年之间。");

        var rows = new List<DdmYearRow>();
        double dividend = input.CurrentDividend;
        double pvDividends = 0;

        for (int year = 1; year <= input.HighGrowthYears; year++)
        {
            double growth = input.LinearFade && input.HighGrowthYears > 1
                ? input.HighGrowthRate + (input.StableGrowthRate - input.HighGrowthRate) * (year - 1) / (input.HighGrowthYears - 1.0)
                : input.HighGrowthRate;
            dividend *= 1 + growth;
            double df = FinancialMath.DiscountFactor(input.CostOfEquity, year);
            double pv = dividend * df;
            pvDividends += pv;
            rows.Add(new DdmYearRow(year, growth, dividend, df, pv));
        }

        double terminalValue = FinancialMath.GordonGrowthValue(
            dividend * (1 + input.StableGrowthRate), input.CostOfEquity, input.StableGrowthRate);
        double terminalDf = FinancialMath.DiscountFactor(input.CostOfEquity, input.HighGrowthYears);
        double pvTerminal = terminalValue * terminalDf;
        double value = pvDividends + pvTerminal;

        double? upside = input.CurrentPrice > 0 ? value / input.CurrentPrice - 1.0 : null;
        double? impliedReturn = input.CurrentPrice > 0 ? SolveImpliedReturn(input) : null;

        string? warning = null;
        if (input.CurrentDividend <= 0)
            warning = "当期股利为 0，DDM 不适用于不分红的公司，建议改用 DCF 或剩余收益模型。";
        else if (input.StableGrowthRate > 0.05)
            warning = "永续增长率高于 5%，长期难以持续，建议下调。";

        return new DdmResult
        {
            Years = rows,
            PresentValueOfDividends = pvDividends,
            TerminalValue = terminalValue,
            PresentValueOfTerminal = pvTerminal,
            IntrinsicValue = value,
            DividendYieldOnCost = value > 0 ? input.CurrentDividend * (1 + (input.HighGrowthYears > 0 ? input.HighGrowthRate : input.StableGrowthRate)) / value : 0,
            UpsidePercent = upside,
            ImpliedReturn = impliedReturn,
            Warning = warning
        };
    }

    private static double? SolveImpliedReturn(DdmInput input)
    {
        double lo = input.StableGrowthRate + 0.0005, hi = 1.0;

        double Value(double r)
        {
            var probe = new DdmInput
            {
                CurrentDividend = input.CurrentDividend,
                HighGrowthYears = input.HighGrowthYears,
                HighGrowthRate = input.HighGrowthRate,
                StableGrowthRate = input.StableGrowthRate,
                CostOfEquity = r,
                LinearFade = input.LinearFade,
                CurrentPrice = 0
            };
            return Calculate(probe).IntrinsicValue;
        }

        double fLo, fHi;
        try { fLo = Value(lo) - input.CurrentPrice; fHi = Value(hi) - input.CurrentPrice; }
        catch { return null; }
        if (fLo * fHi > 0) return null;

        for (int i = 0; i < 200; i++)
        {
            double mid = (lo + hi) / 2.0;
            double fMid = Value(mid) - input.CurrentPrice;
            if (Math.Abs(fMid) < 1e-8) return mid;
            if (fLo * fMid <= 0) hi = mid;
            else { lo = mid; fLo = fMid; }
        }
        return (lo + hi) / 2.0;
    }
}
