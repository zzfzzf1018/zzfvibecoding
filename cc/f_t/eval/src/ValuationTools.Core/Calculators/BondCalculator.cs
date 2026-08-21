namespace ValuationTools.Core.Calculators;

public sealed class BondInput
{
    public double FaceValue { get; init; } = 100;
    /// <summary>票面利率（小数，年化）。</summary>
    public double CouponRate { get; init; }
    public double YearsToMaturity { get; init; }
    /// <summary>到期收益率（小数，年化）。</summary>
    public double YieldToMaturity { get; init; }
    /// <summary>每年付息次数。</summary>
    public int PaymentsPerYear { get; init; } = 1;
    /// <summary>市场价格；大于 0 时反求到期收益率。</summary>
    public double MarketPrice { get; init; }
}

public sealed class BondResult
{
    public double Price { get; init; }
    public double? ImpliedYieldToMaturity { get; init; }
    public double CurrentYield { get; init; }
    public double MacaulayDuration { get; init; }
    public double ModifiedDuration { get; init; }
    public double Convexity { get; init; }
    /// <summary>收益率上行 1% 的估计价格变动百分比（含凸性修正）。</summary>
    public double PriceChangeIfRateUp1Pct { get; init; }
    public double PresentValueOfCoupons { get; init; }
    public double PresentValueOfPrincipal { get; init; }
    public string Judgement { get; init; } = string.Empty;
}

/// <summary>债券估值：价格、到期收益率、久期与凸性。</summary>
public static class BondCalculator
{
    public static BondResult Calculate(BondInput input)
    {
        int frequency = Math.Max(input.PaymentsPerYear, 1);
        int periods = (int)Math.Round(input.YearsToMaturity * frequency);
        if (periods <= 0) throw new ArgumentException("剩余期限必须大于 0。");
        if (periods > 1200) throw new ArgumentException("剩余期限 × 付息频率不能超过 1200 期。");

        double couponPayment = input.FaceValue * input.CouponRate / frequency;
        double yieldUsed = input.MarketPrice > 0
            ? SolveYield(input.FaceValue, couponPayment, periods, frequency, input.MarketPrice) ?? input.YieldToMaturity
            : input.YieldToMaturity;

        double periodicYield = yieldUsed / frequency;
        double pvCoupons = 0, weightedTime = 0, convexitySum = 0;

        for (int t = 1; t <= periods; t++)
        {
            double df = Math.Pow(1 + periodicYield, -t);
            double cash = couponPayment + (t == periods ? input.FaceValue : 0);
            double pv = cash * df;
            pvCoupons += couponPayment * df;
            weightedTime += (t / (double)frequency) * pv;
            convexitySum += pv * t * (t + 1) / Math.Pow(1 + periodicYield, 2);
        }

        double pvPrincipal = input.FaceValue * Math.Pow(1 + periodicYield, -periods);
        double price = pvCoupons + pvPrincipal;

        double macaulay = price > 0 ? weightedTime / price : 0;
        double modified = macaulay / (1 + periodicYield);
        double convexity = price > 0 ? convexitySum / (price * frequency * frequency) : 0;
        double change = -modified * 0.01 + 0.5 * convexity * 0.01 * 0.01;

        double referencePrice = input.MarketPrice > 0 ? input.MarketPrice : price;
        string judgement = referencePrice > input.FaceValue ? "溢价发行/交易（票息高于市场利率）"
            : referencePrice < input.FaceValue ? "折价发行/交易（票息低于市场利率）"
            : "平价";

        return new BondResult
        {
            Price = price,
            ImpliedYieldToMaturity = input.MarketPrice > 0 ? yieldUsed : null,
            CurrentYield = referencePrice > 0 ? input.FaceValue * input.CouponRate / referencePrice : 0,
            MacaulayDuration = macaulay,
            ModifiedDuration = modified,
            Convexity = convexity,
            PriceChangeIfRateUp1Pct = change,
            PresentValueOfCoupons = pvCoupons,
            PresentValueOfPrincipal = pvPrincipal,
            Judgement = judgement
        };
    }

    private static double? SolveYield(double face, double couponPayment, int periods, int frequency, double marketPrice)
    {
        double lo = -0.99, hi = 5.0;

        double PriceAt(double annualYield)
        {
            double periodic = annualYield / frequency;
            double sum = 0;
            for (int t = 1; t <= periods; t++)
                sum += (couponPayment + (t == periods ? face : 0)) * Math.Pow(1 + periodic, -t);
            return sum;
        }

        double fLo = PriceAt(lo) - marketPrice;
        double fHi = PriceAt(hi) - marketPrice;
        if (fLo * fHi > 0) return null;

        for (int i = 0; i < 300; i++)
        {
            double mid = (lo + hi) / 2;
            double fMid = PriceAt(mid) - marketPrice;
            if (Math.Abs(fMid) < 1e-10) return mid;
            if (fLo * fMid <= 0) hi = mid;
            else { lo = mid; fLo = fMid; }
        }
        return (lo + hi) / 2;
    }
}
