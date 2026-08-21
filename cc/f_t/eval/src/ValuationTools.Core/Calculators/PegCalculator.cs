namespace ValuationTools.Core.Calculators;

public sealed class PegInput
{
    public double Price { get; init; }
    /// <summary>每股收益（TTM 或预测）。</summary>
    public double EarningsPerShare { get; init; }
    /// <summary>直接给定的市盈率；大于 0 时优先于 Price/EPS。</summary>
    public double GivenPeRatio { get; init; }
    /// <summary>未来若干年的预期盈利年复合增长率（小数）。</summary>
    public double EarningsGrowthRate { get; init; }
    public double DividendYield { get; init; }
    /// <summary>合理 PEG 基准，通常为 1。</summary>
    public double TargetPeg { get; init; } = 1.0;
    /// <summary>用于推算目标价的持有年限。</summary>
    public int HoldingYears { get; init; } = 3;
}

public sealed class PegResult
{
    public double PeRatio { get; init; }
    public double Peg { get; init; }
    /// <summary>考虑股息的 PEGY = PE / (增长率% + 股息率%)。</summary>
    public double Pegy { get; init; }
    public double FairPeRatio { get; init; }
    public double FairPrice { get; init; }
    public double UpsidePercent { get; init; }
    /// <summary>当前价格隐含的盈利增长率。</summary>
    public double ImpliedGrowthRate { get; init; }
    public double ForwardEps { get; init; }
    /// <summary>持有期末按合理 PE 计算的目标价。</summary>
    public double TargetPrice { get; init; }
    /// <summary>持有期的年化回报（含股息）。</summary>
    public double ExpectedAnnualReturn { get; init; }
    public string Judgement { get; init; } = string.Empty;
    public string? Warning { get; init; }
}

/// <summary>PEG（市盈率相对盈利增长比率）估值工具。</summary>
public static class PegCalculator
{
    public static PegResult Calculate(PegInput input)
    {
        double pe = input.GivenPeRatio > 0
            ? input.GivenPeRatio
            : (input.EarningsPerShare > 0 && input.Price > 0 ? input.Price / input.EarningsPerShare : 0);

        if (pe <= 0)
            throw new ArgumentException("无法得到有效市盈率，请填写股价与每股收益，或直接填写市盈率。");

        double growthPercent = input.EarningsGrowthRate * 100.0;
        if (Math.Abs(growthPercent) < 1e-6)
            throw new ArgumentException("盈利增长率不能为 0，PEG 无意义。");

        double peg = pe / growthPercent;
        double pegy = pe / (growthPercent + input.DividendYield * 100.0);

        double targetPeg = input.TargetPeg > 0 ? input.TargetPeg : 1.0;
        double fairPe = growthPercent * targetPeg;
        double eps = input.EarningsPerShare > 0
            ? input.EarningsPerShare
            : (input.Price > 0 ? input.Price / pe : 0);
        double fairPrice = fairPe * eps;

        int years = Math.Max(input.HoldingYears, 1);
        double forwardEps = eps * Math.Pow(1 + input.EarningsGrowthRate, years);
        double targetPrice = forwardEps * fairPe;
        double currentPrice = input.Price > 0 ? input.Price : eps * pe;

        double annualReturn = currentPrice > 0 && targetPrice > 0
            ? Math.Pow(targetPrice / currentPrice, 1.0 / years) - 1.0 + input.DividendYield
            : 0;

        string judgement = peg switch
        {
            < 0 => "增长率为负，PEG 不适用",
            < 0.75 => "PEG 明显低于 1，可能被低估（需确认增长可持续）",
            < 1.2 => "PEG 接近 1，估值基本合理",
            < 2 => "PEG 偏高，需要更强的增长确定性支撑",
            _ => "PEG 显著高于 2，估值偏贵"
        };

        string? warning = null;
        if (input.EarningsGrowthRate > 0.5)
            warning = "预期增长率超过 50%，PEG 容易高估合理估值，建议用 DCF 交叉验证。";
        else if (input.EarningsGrowthRate < 0)
            warning = "增长率为负时 PEG 失效，请改用 PB、股息折现或清算价值等方法。";

        return new PegResult
        {
            PeRatio = pe,
            Peg = peg,
            Pegy = pegy,
            FairPeRatio = fairPe,
            FairPrice = fairPrice,
            UpsidePercent = currentPrice > 0 ? fairPrice / currentPrice - 1.0 : 0,
            ImpliedGrowthRate = pe / targetPeg / 100.0,
            ForwardEps = forwardEps,
            TargetPrice = targetPrice,
            ExpectedAnnualReturn = annualReturn,
            Judgement = judgement,
            Warning = warning
        };
    }
}
