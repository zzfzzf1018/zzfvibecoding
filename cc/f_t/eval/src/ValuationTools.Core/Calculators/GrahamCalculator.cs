namespace ValuationTools.Core.Calculators;

public sealed class GrahamInput
{
    public double EarningsPerShare { get; init; }
    public double BookValuePerShare { get; init; }
    /// <summary>未来 7-10 年预期盈利增长率（小数）。</summary>
    public double GrowthRate { get; init; }
    /// <summary>当前 AAA 级公司债收益率（小数），用于修正公式。</summary>
    public double CorporateBondYield { get; init; } = 0.044;
    public double CurrentPrice { get; init; }

    public double CurrentAssets { get; init; }
    public double TotalLiabilities { get; init; }
    public double SharesOutstanding { get; init; }
    public double MarginOfSafety { get; init; } = 0.3;
}

public sealed class GrahamResult
{
    /// <summary>原始公式 V = EPS × (8.5 + 2g)。</summary>
    public double ClassicValue { get; init; }
    /// <summary>修正公式 V = EPS × (8.5 + 2g) × 4.4 / Y。</summary>
    public double RevisedValue { get; init; }
    /// <summary>格雷厄姆数 = √(22.5 × EPS × BVPS)。</summary>
    public double GrahamNumber { get; init; }
    /// <summary>每股净流动资产价值（NCAV）。</summary>
    public double NetCurrentAssetValuePerShare { get; init; }
    /// <summary>NCAV 的三分之二，格雷厄姆的经典买入线。</summary>
    public double NcavBuyPrice { get; init; }
    public double BuyBelowPrice { get; init; }
    public double? UpsidePercent { get; init; }
    public string Judgement { get; init; } = string.Empty;
    public string? Warning { get; init; }
}

/// <summary>格雷厄姆内在价值公式与格雷厄姆数。</summary>
public static class GrahamCalculator
{
    public static GrahamResult Calculate(GrahamInput input)
    {
        double growthPercent = input.GrowthRate * 100.0;
        double classic = input.EarningsPerShare * (8.5 + 2 * growthPercent);

        double bondYieldPercent = input.CorporateBondYield * 100.0;
        double revised = bondYieldPercent > 0
            ? classic * 4.4 / bondYieldPercent
            : classic;

        double grahamNumber = input.EarningsPerShare > 0 && input.BookValuePerShare > 0
            ? Math.Sqrt(22.5 * input.EarningsPerShare * input.BookValuePerShare)
            : 0;

        double ncavPerShare = input.SharesOutstanding > 0
            ? (input.CurrentAssets - input.TotalLiabilities) / input.SharesOutstanding
            : 0;

        double reference = revised > 0 ? revised : classic;
        double buyBelow = reference * (1 - input.MarginOfSafety);
        double? upside = input.CurrentPrice > 0 && reference != 0 ? reference / input.CurrentPrice - 1.0 : null;

        string judgement = upside switch
        {
            null => "填写当前股价后可给出判断",
            > 0.3 => "低于内在价值 30% 以上，符合格雷厄姆的安全边际标准",
            > 0 => "略低于内在价值，安全边际不足",
            _ => "高于内在价值，不符合价值投资买入条件"
        };

        string? warning = null;
        if (input.EarningsPerShare <= 0)
            warning = "每股收益为负或为 0，格雷厄姆公式不适用。";
        else if (input.GrowthRate > 0.15)
            warning = "格雷厄姆公式对高增长非常敏感（增长率线性放大 PE），增长率超过 15% 时结果易失真。";

        return new GrahamResult
        {
            ClassicValue = classic,
            RevisedValue = revised,
            GrahamNumber = grahamNumber,
            NetCurrentAssetValuePerShare = ncavPerShare,
            NcavBuyPrice = ncavPerShare * 2.0 / 3.0,
            BuyBelowPrice = buyBelow,
            UpsidePercent = upside,
            Judgement = judgement,
            Warning = warning
        };
    }
}
