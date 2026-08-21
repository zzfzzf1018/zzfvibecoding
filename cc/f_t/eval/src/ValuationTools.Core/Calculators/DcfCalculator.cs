using ValuationTools.Core.Common;

namespace ValuationTools.Core.Calculators;

/// <summary>终值计算方式。</summary>
public enum TerminalValueMethod
{
    /// <summary>戈登永续增长模型。</summary>
    GordonGrowth = 0,
    /// <summary>退出倍数法。</summary>
    ExitMultiple = 1
}

public sealed class DcfInput
{
    /// <summary>基期自由现金流（FCFF 或 FCFE）。</summary>
    public double BaseCashFlow { get; init; }
    public int Stage1Years { get; init; } = 5;
    /// <summary>第一阶段年增长率（小数）。</summary>
    public double Stage1Growth { get; init; }
    public int Stage2Years { get; init; }
    public double Stage2Growth { get; init; }
    /// <summary>折现率（FCFF 用 WACC，FCFE 用股权成本）。</summary>
    public double DiscountRate { get; init; }
    public double TerminalGrowth { get; init; }
    public TerminalValueMethod TerminalMethod { get; init; } = TerminalValueMethod.GordonGrowth;
    /// <summary>退出倍数（如 EV/EBITDA）。</summary>
    public double ExitMultiple { get; init; }
    /// <summary>退出倍数对应的终期指标（如末年 EBITDA）；不填则使用末年现金流。</summary>
    public double TerminalMetric { get; init; }
    /// <summary>是否采用期中折现法。</summary>
    public bool MidYearConvention { get; init; }
    /// <summary>净债务（有息负债 - 现金）。折现 FCFE 时应填 0。</summary>
    public double NetDebt { get; init; }
    public double SharesOutstanding { get; init; }
    public double CurrentPrice { get; init; }
    /// <summary>安全边际（小数），用于计算买入价。</summary>
    public double MarginOfSafety { get; init; }
}

public sealed record DcfYearRow(
    int Year,
    double GrowthRate,
    double CashFlow,
    double DiscountFactor,
    double PresentValue);

public sealed class DcfResult
{
    public IReadOnlyList<DcfYearRow> Years { get; init; } = Array.Empty<DcfYearRow>();
    public double PresentValueOfForecast { get; init; }
    public double TerminalValue { get; init; }
    public double PresentValueOfTerminal { get; init; }
    public double EnterpriseValue { get; init; }
    public double EquityValue { get; init; }
    public double ValuePerShare { get; init; }
    /// <summary>终值现值占企业价值的比重。</summary>
    public double TerminalWeight { get; init; }
    public double? UpsidePercent { get; init; }
    public double BuyBelowPrice { get; init; }
    /// <summary>使当前股价成立的隐含永续增长率。</summary>
    public double? ImpliedTerminalGrowth { get; init; }
    public string? Warning { get; init; }
}

/// <summary>多阶段现金流折现（DCF）模型。</summary>
public static class DcfCalculator
{
    public static DcfResult Calculate(DcfInput input)
    {
        if (input.Stage1Years < 0 || input.Stage2Years < 0)
            throw new ArgumentException("预测年数不能为负数。");
        int totalYears = input.Stage1Years + input.Stage2Years;
        if (totalYears == 0)
            throw new ArgumentException("至少需要一年的明确预测期。");
        if (totalYears > 100)
            throw new ArgumentException("预测期合计不能超过 100 年。");
        if (input.DiscountRate <= -1)
            throw new ArgumentException("折现率必须大于 -100%。");

        var rows = new List<DcfYearRow>(totalYears);
        double cashFlow = input.BaseCashFlow;
        double pvForecast = 0;

        for (int year = 1; year <= totalYears; year++)
        {
            double growth = year <= input.Stage1Years ? input.Stage1Growth : input.Stage2Growth;
            cashFlow *= 1.0 + growth;
            double period = input.MidYearConvention ? year - 0.5 : year;
            double df = FinancialMath.DiscountFactor(input.DiscountRate, period);
            double pv = cashFlow * df;
            pvForecast += pv;
            rows.Add(new DcfYearRow(year, growth, cashFlow, df, pv));
        }

        string? warning = null;
        double terminalValue;
        if (input.TerminalMethod == TerminalValueMethod.GordonGrowth)
        {
            if (input.DiscountRate - input.TerminalGrowth <= 1e-6)
                throw new InvalidOperationException("折现率必须大于永续增长率，否则终值无意义。");
            if (input.TerminalGrowth > 0.05)
                warning = "永续增长率高于 5%，长期看很难超过名义 GDP 增速，建议下调。";
            terminalValue = FinancialMath.GordonGrowthValue(cashFlow * (1 + input.TerminalGrowth), input.DiscountRate, input.TerminalGrowth);
        }
        else
        {
            double metric = input.TerminalMetric > 0 ? input.TerminalMetric : cashFlow;
            terminalValue = metric * input.ExitMultiple;
        }

        double terminalDf = FinancialMath.DiscountFactor(input.DiscountRate, totalYears);
        double pvTerminal = terminalValue * terminalDf;
        double enterpriseValue = pvForecast + pvTerminal;
        double equityValue = enterpriseValue - input.NetDebt;
        double perShare = input.SharesOutstanding > 0 ? equityValue / input.SharesOutstanding : 0;

        double terminalWeight = Math.Abs(enterpriseValue) > 1e-9 ? pvTerminal / enterpriseValue : 0;
        if (input.SharesOutstanding <= 0)
            warning = "未填写总股本，无法折算每股价值。";
        else if (warning is null && terminalWeight > 0.8)
            warning = $"终值占企业价值 {terminalWeight:P1}，估值高度依赖长期假设，结果敏感性大。";

        double? upside = input.CurrentPrice > 0 && perShare != 0
            ? perShare / input.CurrentPrice - 1.0
            : null;

        return new DcfResult
        {
            Years = rows,
            PresentValueOfForecast = pvForecast,
            TerminalValue = terminalValue,
            PresentValueOfTerminal = pvTerminal,
            EnterpriseValue = enterpriseValue,
            EquityValue = equityValue,
            ValuePerShare = perShare,
            TerminalWeight = terminalWeight,
            UpsidePercent = upside,
            BuyBelowPrice = perShare * (1 - input.MarginOfSafety),
            ImpliedTerminalGrowth = SolveImpliedTerminalGrowth(input),
            Warning = warning
        };
    }

    /// <summary>反推：当前股价隐含的永续增长率（二分法）。</summary>
    private static double? SolveImpliedTerminalGrowth(DcfInput input)
    {
        if (input.CurrentPrice <= 0 || input.SharesOutstanding <= 0) return null;
        if (input.TerminalMethod != TerminalValueMethod.GordonGrowth) return null;

        double lo = -0.5, hi = input.DiscountRate - 0.0005;
        if (hi <= lo) return null;

        double Price(double g)
        {
            var probe = CloneWith(input, g);
            var result = Calculate(probe);
            return result.ValuePerShare;
        }

        double fLo, fHi;
        try { fLo = Price(lo) - input.CurrentPrice; fHi = Price(hi) - input.CurrentPrice; }
        catch { return null; }
        if (fLo * fHi > 0) return null;

        for (int i = 0; i < 200; i++)
        {
            double mid = (lo + hi) / 2.0;
            double fMid = Price(mid) - input.CurrentPrice;
            if (Math.Abs(fMid) < 1e-7) return mid;
            if (fLo * fMid <= 0) hi = mid;
            else { lo = mid; fLo = fMid; }
        }
        return (lo + hi) / 2.0;
    }

    private static DcfInput CloneWith(DcfInput source, double terminalGrowth) => new()
    {
        BaseCashFlow = source.BaseCashFlow,
        Stage1Years = source.Stage1Years,
        Stage1Growth = source.Stage1Growth,
        Stage2Years = source.Stage2Years,
        Stage2Growth = source.Stage2Growth,
        DiscountRate = source.DiscountRate,
        TerminalGrowth = terminalGrowth,
        TerminalMethod = source.TerminalMethod,
        ExitMultiple = source.ExitMultiple,
        TerminalMetric = source.TerminalMetric,
        MidYearConvention = source.MidYearConvention,
        NetDebt = source.NetDebt,
        SharesOutstanding = source.SharesOutstanding,
        CurrentPrice = 0,
        MarginOfSafety = source.MarginOfSafety
    };
}
