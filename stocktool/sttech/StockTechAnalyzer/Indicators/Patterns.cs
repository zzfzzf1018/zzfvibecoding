using StockTechAnalyzer.Models;

namespace StockTechAnalyzer.Indicators;

/// <summary>
/// 经典 K 线形态识别（基于近 1-3 根 K 线的几何关系）。
/// </summary>
public static class Patterns
{
    public sealed record Hit(int Index, DateTime Date, string Name, bool Bullish, string Description);

    public static IReadOnlyList<Hit> Detect(IReadOnlyList<Kline> bars)
    {
        var hits = new List<Hit>();
        if (bars.Count < 5) return hits;

        for (int i = 2; i < bars.Count; i++)
        {
            var b0 = bars[i - 2];
            var b1 = bars[i - 1];
            var b = bars[i];

            // 单根
            if (IsHammer(b))
                hits.Add(new Hit(i, b.Date, "锤子线", true, "下影线长，可能止跌反弹（需位于下跌趋势末端更可靠）"));
            if (IsHangingMan(b))
                hits.Add(new Hit(i, b.Date, "上吊线", false, "形态同锤子但位于上涨末端，警惕见顶"));
            if (IsShootingStar(b))
                hits.Add(new Hit(i, b.Date, "射击之星", false, "上影线长，上方抛压重，警惕回落"));
            if (IsDoji(b))
                hits.Add(new Hit(i, b.Date, "十字星", true, "多空力量均衡，可能变盘"));

            // 双根
            if (IsBullishEngulfing(b1, b))
                hits.Add(new Hit(i, b.Date, "看涨吞没", true, "阳线完全吞没前一阴线，反转信号"));
            if (IsBearishEngulfing(b1, b))
                hits.Add(new Hit(i, b.Date, "看跌吞没", false, "阴线完全吞没前一阳线，反转信号"));

            // 三根
            if (IsMorningStar(b0, b1, b))
                hits.Add(new Hit(i, b.Date, "启明星", true, "底部三根 K 线反转组合，看涨"));
            if (IsEveningStar(b0, b1, b))
                hits.Add(new Hit(i, b.Date, "黄昏星", false, "顶部三根 K 线反转组合，看跌"));
            if (i >= 2 && IsThreeWhiteSoldiers(b0, b1, b))
                hits.Add(new Hit(i, b.Date, "红三兵", true, "连续三阳上攻，多头持续"));
            if (i >= 2 && IsThreeBlackCrows(b0, b1, b))
                hits.Add(new Hit(i, b.Date, "三只乌鸦", false, "连续三阴下挫，空头持续"));
        }
        return hits;
    }

    // -------- 单根 --------
    private static double Body(Kline k) => Math.Abs(k.Close - k.Open);
    private static double Range(Kline k) => Math.Max(1e-9, k.High - k.Low);
    private static double UpShadow(Kline k) => k.High - Math.Max(k.Open, k.Close);
    private static double LowShadow(Kline k) => Math.Min(k.Open, k.Close) - k.Low;
    private static bool IsBull(Kline k) => k.Close > k.Open;
    private static bool IsBear(Kline k) => k.Close < k.Open;

    private static bool IsHammer(Kline k)
    {
        var body = Body(k);
        var rng = Range(k);
        return body / rng < 0.35
            && LowShadow(k) > body * 2
            && UpShadow(k) < body * 0.5;
    }

    private static bool IsHangingMan(Kline k) => IsHammer(k); // 同形，位置不同

    private static bool IsShootingStar(Kline k)
    {
        var body = Body(k);
        var rng = Range(k);
        return body / rng < 0.35
            && UpShadow(k) > body * 2
            && LowShadow(k) < body * 0.5;
    }

    private static bool IsDoji(Kline k)
    {
        var rng = Range(k);
        return Body(k) / rng < 0.10;
    }

    // -------- 双根 --------
    private static bool IsBullishEngulfing(Kline prev, Kline cur)
        => IsBear(prev) && IsBull(cur)
            && cur.Open <= prev.Close && cur.Close >= prev.Open
            && Body(cur) > Body(prev);

    private static bool IsBearishEngulfing(Kline prev, Kline cur)
        => IsBull(prev) && IsBear(cur)
            && cur.Open >= prev.Close && cur.Close <= prev.Open
            && Body(cur) > Body(prev);

    // -------- 三根 --------
    private static bool IsMorningStar(Kline a, Kline b, Kline c)
    {
        if (!IsBear(a)) return false;
        if (Body(b) > Body(a) * 0.5) return false; // 中间小实体
        if (!IsBull(c)) return false;
        return c.Close > (a.Open + a.Close) / 2;
    }

    private static bool IsEveningStar(Kline a, Kline b, Kline c)
    {
        if (!IsBull(a)) return false;
        if (Body(b) > Body(a) * 0.5) return false;
        if (!IsBear(c)) return false;
        return c.Close < (a.Open + a.Close) / 2;
    }

    private static bool IsThreeWhiteSoldiers(Kline a, Kline b, Kline c)
        => IsBull(a) && IsBull(b) && IsBull(c)
            && b.Close > a.Close && c.Close > b.Close
            && b.Open > a.Open && b.Open < a.Close
            && c.Open > b.Open && c.Open < b.Close;

    private static bool IsThreeBlackCrows(Kline a, Kline b, Kline c)
        => IsBear(a) && IsBear(b) && IsBear(c)
            && b.Close < a.Close && c.Close < b.Close
            && b.Open < a.Open && b.Open > a.Close
            && c.Open < b.Open && c.Open > b.Close;
}
