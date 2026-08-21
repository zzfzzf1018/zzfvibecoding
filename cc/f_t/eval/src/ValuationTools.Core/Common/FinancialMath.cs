namespace ValuationTools.Core.Common;

/// <summary>
/// 通用财务数学函数：折现、净现值、内部收益率、复合增长率等。
/// </summary>
public static class FinancialMath
{
    /// <summary>第 period 期的折现因子（period 可为小数，用于期中折现）。</summary>
    public static double DiscountFactor(double rate, double period)
    {
        if (rate <= -1) throw new ArgumentOutOfRangeException(nameof(rate), "折现率必须大于 -100%。");
        return 1.0 / Math.Pow(1.0 + rate, period);
    }

    /// <summary>现金流序列的现值，第一笔发生在 firstPeriod 期末。</summary>
    public static double PresentValue(double rate, IReadOnlyList<double> cashFlows, double firstPeriod = 1.0)
    {
        double sum = 0;
        for (int i = 0; i < cashFlows.Count; i++)
            sum += cashFlows[i] * DiscountFactor(rate, firstPeriod + i);
        return sum;
    }

    /// <summary>净现值，cashFlows[0] 为 t=0 时点现金流（通常为负的初始投资）。</summary>
    public static double Npv(double rate, IReadOnlyList<double> cashFlows)
    {
        double sum = 0;
        for (int i = 0; i < cashFlows.Count; i++)
            sum += cashFlows[i] * DiscountFactor(rate, i);
        return sum;
    }

    /// <summary>内部收益率，使用二分法求解；无解时返回 null。cashFlows[0] 为 t=0 现金流。</summary>
    public static double? Irr(IReadOnlyList<double> cashFlows, double tolerance = 1e-9, int maxIterations = 500)
    {
        if (cashFlows.Count < 2) return null;
        if (cashFlows.All(c => c >= 0) || cashFlows.All(c => c <= 0)) return null;

        double lo = -0.9999, hi = 10.0;
        double fLo = Npv(lo, cashFlows);
        double fHi = Npv(hi, cashFlows);

        // 扩大上界寻找符号变化
        int expand = 0;
        while (fLo * fHi > 0 && expand++ < 20)
        {
            hi *= 2;
            fHi = Npv(hi, cashFlows);
        }
        if (fLo * fHi > 0) return null;

        for (int i = 0; i < maxIterations; i++)
        {
            double mid = (lo + hi) / 2.0;
            double fMid = Npv(mid, cashFlows);
            if (Math.Abs(fMid) < tolerance || (hi - lo) / 2.0 < tolerance) return mid;
            if (fLo * fMid <= 0) { hi = mid; }
            else { lo = mid; fLo = fMid; }
        }
        return (lo + hi) / 2.0;
    }

    /// <summary>复合年化增长率 CAGR。</summary>
    public static double? Cagr(double beginValue, double endValue, double years)
    {
        if (years <= 0 || beginValue <= 0 || endValue <= 0) return null;
        return Math.Pow(endValue / beginValue, 1.0 / years) - 1.0;
    }

    /// <summary>戈登永续增长模型：下一期现金流 / (折现率 - 永续增长率)。</summary>
    public static double GordonGrowthValue(double nextPeriodCashFlow, double rate, double growth)
    {
        if (rate - growth <= 1e-9) throw new InvalidOperationException("折现率必须显著大于永续增长率，否则模型不成立。");
        return nextPeriodCashFlow / (rate - growth);
    }

    /// <summary>回收期（年），线性插值；未回收返回 null。</summary>
    public static double? PaybackPeriod(IReadOnlyList<double> cashFlows)
    {
        double cumulative = cashFlows[0];
        if (cumulative >= 0) return 0;
        for (int i = 1; i < cashFlows.Count; i++)
        {
            double previous = cumulative;
            cumulative += cashFlows[i];
            if (cumulative >= 0)
                return cashFlows[i] == 0 ? i : (i - 1) + (-previous / cashFlows[i]);
        }
        return null;
    }

    /// <summary>正态分布累积概率函数（Abramowitz &amp; Stegun 近似）。</summary>
    public static double NormalCdf(double x)
    {
        const double a1 = 0.254829592, a2 = -0.284496736, a3 = 1.421413741,
                     a4 = -1.453152027, a5 = 1.061405429, p = 0.3275911;
        int sign = x < 0 ? -1 : 1;
        x = Math.Abs(x) / Math.Sqrt(2.0);
        double t = 1.0 / (1.0 + p * x);
        double y = 1.0 - (((((a5 * t + a4) * t) + a3) * t + a2) * t + a1) * t * Math.Exp(-x * x);
        return 0.5 * (1.0 + sign * y);
    }

    /// <summary>标准正态分布概率密度函数。</summary>
    public static double NormalPdf(double x) => Math.Exp(-0.5 * x * x) / Math.Sqrt(2.0 * Math.PI);
}
