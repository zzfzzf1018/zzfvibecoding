using ValuationTools.Core.Common;

namespace ValuationTools.Core.Calculators;

public sealed class OptionInput
{
    /// <summary>标的现价（实物期权中为项目现值）。</summary>
    public double SpotPrice { get; init; }
    /// <summary>行权价（实物期权中为投资成本）。</summary>
    public double StrikePrice { get; init; }
    /// <summary>到期时间（年）。</summary>
    public double TimeToMaturity { get; init; }
    /// <summary>无风险利率（连续复利，小数）。</summary>
    public double RiskFreeRate { get; init; }
    /// <summary>年化波动率（小数）。</summary>
    public double Volatility { get; init; }
    /// <summary>股息率 / 便利收益（小数）。</summary>
    public double DividendYield { get; init; }
}

public sealed class OptionResult
{
    public double CallPrice { get; init; }
    public double PutPrice { get; init; }
    public double D1 { get; init; }
    public double D2 { get; init; }
    public double CallDelta { get; init; }
    public double PutDelta { get; init; }
    public double Gamma { get; init; }
    /// <summary>Vega：波动率上升 1 个百分点的价格变动。</summary>
    public double Vega { get; init; }
    /// <summary>Theta：每日时间价值损耗。</summary>
    public double CallTheta { get; init; }
    public double CallRho { get; init; }
    /// <summary>行权概率（风险中性下 N(d2)）。</summary>
    public double ExerciseProbability { get; init; }
}

/// <summary>Black-Scholes-Merton 期权定价，可用于实物期权与可转债期权部分估值。</summary>
public static class OptionCalculator
{
    public static OptionResult Calculate(OptionInput input)
    {
        if (input.SpotPrice <= 0 || input.StrikePrice <= 0)
            throw new ArgumentException("标的价格与行权价必须大于 0。");
        if (input.TimeToMaturity <= 0)
            throw new ArgumentException("到期时间必须大于 0。");
        if (input.Volatility <= 0)
            throw new ArgumentException("波动率必须大于 0。");

        double s = input.SpotPrice, k = input.StrikePrice, t = input.TimeToMaturity;
        double r = input.RiskFreeRate, q = input.DividendYield, sigma = input.Volatility;
        double sqrtT = Math.Sqrt(t);

        double d1 = (Math.Log(s / k) + (r - q + 0.5 * sigma * sigma) * t) / (sigma * sqrtT);
        double d2 = d1 - sigma * sqrtT;

        double discountR = Math.Exp(-r * t);
        double discountQ = Math.Exp(-q * t);

        double call = s * discountQ * FinancialMath.NormalCdf(d1) - k * discountR * FinancialMath.NormalCdf(d2);
        double put = k * discountR * FinancialMath.NormalCdf(-d2) - s * discountQ * FinancialMath.NormalCdf(-d1);

        double pdfD1 = FinancialMath.NormalPdf(d1);
        double theta = (-s * discountQ * pdfD1 * sigma / (2 * sqrtT)
                        - r * k * discountR * FinancialMath.NormalCdf(d2)
                        + q * s * discountQ * FinancialMath.NormalCdf(d1)) / 365.0;

        return new OptionResult
        {
            CallPrice = call,
            PutPrice = put,
            D1 = d1,
            D2 = d2,
            CallDelta = discountQ * FinancialMath.NormalCdf(d1),
            PutDelta = discountQ * (FinancialMath.NormalCdf(d1) - 1),
            Gamma = discountQ * pdfD1 / (s * sigma * sqrtT),
            Vega = s * discountQ * pdfD1 * sqrtT / 100.0,
            CallTheta = theta,
            CallRho = k * t * discountR * FinancialMath.NormalCdf(d2) / 100.0,
            ExerciseProbability = FinancialMath.NormalCdf(d2)
        };
    }
}
