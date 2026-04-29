using System.Text;
using StockTechAnalyzer.Indicators;
using StockTechAnalyzer.Models;

namespace StockTechAnalyzer.Analysis;

/// <summary>
/// 综合多个技术指标输出市场情绪评分（-100 ~ +100）。
/// 评分越高越偏多，越低越偏空。
/// </summary>
public static class SentimentAnalyzer
{
    public sealed record Report(
        double Score,            // 综合分 -100..100
        string Sentiment,        // 文字描述
        string Recommendation,   // 操作建议
        IReadOnlyList<string> Signals,  // 各项触发信号
        IReadOnlyList<(string Name, double Score)> Components);

    public static Report Analyze(IReadOnlyList<Kline> bars, ChipDistribution.Result? chips = null)
    {
        var signals = new List<string>();
        var comps = new List<(string, double)>();

        if (bars.Count < 30)
        {
            return new Report(0, "数据不足", "—", signals, comps);
        }

        var close = bars.Select(b => b.Close).ToArray();
        int n = close.Length;
        double last = close[^1];

        // ---- 趋势：MA 多头/空头排列 ----
        var ma5 = Indicators.Indicators.SMA(close, 5);
        var ma10 = Indicators.Indicators.SMA(close, 10);
        var ma20 = Indicators.Indicators.SMA(close, 20);
        var ma60 = Indicators.Indicators.SMA(close, 60);
        double trendScore = 0;
        if (!double.IsNaN(ma60[^1]))
        {
            if (ma5[^1] > ma10[^1] && ma10[^1] > ma20[^1] && ma20[^1] > ma60[^1])
            { trendScore = 80; signals.Add("均线多头排列：MA5>MA10>MA20>MA60"); }
            else if (ma5[^1] < ma10[^1] && ma10[^1] < ma20[^1] && ma20[^1] < ma60[^1])
            { trendScore = -80; signals.Add("均线空头排列：MA5<MA10<MA20<MA60"); }
            else
            {
                int up = 0;
                if (last > ma5[^1]) up++;
                if (last > ma20[^1]) up++;
                if (last > ma60[^1]) up++;
                trendScore = (up - 1.5) * 30;
                signals.Add($"均线分歧：当前价位于 {up}/3 条均线之上");
            }
        }
        comps.Add(("趋势(均线)", trendScore));

        // ---- MACD ----
        var macd = Indicators.Indicators.MACD(close);
        double macdScore = 0;
        double hLast = macd.Hist[^1], hPrev = macd.Hist[^2];
        if (hLast > 0 && hPrev <= 0) { macdScore = 70; signals.Add("MACD 金叉：柱由负转正"); }
        else if (hLast < 0 && hPrev >= 0) { macdScore = -70; signals.Add("MACD 死叉：柱由正转负"); }
        else if (hLast > 0 && hLast > hPrev) { macdScore = 40; signals.Add("MACD 红柱放大，多头延续"); }
        else if (hLast > 0 && hLast < hPrev) { macdScore = 15; signals.Add("MACD 红柱缩短，多头动能减弱"); }
        else if (hLast < 0 && hLast < hPrev) { macdScore = -40; signals.Add("MACD 绿柱放大，空头延续"); }
        else if (hLast < 0 && hLast > hPrev) { macdScore = -15; signals.Add("MACD 绿柱缩短，空头动能减弱"); }
        comps.Add(("MACD", macdScore));

        // ---- KDJ ----
        var kdj = Indicators.Indicators.KDJ(bars);
        double k = kdj.K[^1], d = kdj.D[^1], j = kdj.J[^1];
        double kdjScore = 0;
        if (j > 100) { kdjScore = -40; signals.Add($"KDJ 超买 (J={j:F1})"); }
        else if (j < 0) { kdjScore = 40; signals.Add($"KDJ 超卖 (J={j:F1})"); }
        else if (k > d && kdj.K[^2] <= kdj.D[^2]) { kdjScore = 50; signals.Add("KDJ 金叉"); }
        else if (k < d && kdj.K[^2] >= kdj.D[^2]) { kdjScore = -50; signals.Add("KDJ 死叉"); }
        else kdjScore = (k - 50) * 0.6;
        comps.Add(("KDJ", kdjScore));

        // ---- RSI ----
        var rsi = Indicators.Indicators.RSI(close);
        double rsiV = rsi[^1];
        double rsiScore = 0;
        if (!double.IsNaN(rsiV))
        {
            if (rsiV > 80) { rsiScore = -50; signals.Add($"RSI 严重超买 ({rsiV:F1})"); }
            else if (rsiV > 70) { rsiScore = -25; signals.Add($"RSI 超买 ({rsiV:F1})"); }
            else if (rsiV < 20) { rsiScore = 50; signals.Add($"RSI 严重超卖 ({rsiV:F1})"); }
            else if (rsiV < 30) { rsiScore = 25; signals.Add($"RSI 超卖 ({rsiV:F1})"); }
            else rsiScore = (rsiV - 50) * 0.8;
        }
        comps.Add(("RSI", rsiScore));

        // ---- BOLL ----
        var boll = Indicators.Indicators.BOLL(close);
        double bollScore = 0;
        if (!double.IsNaN(boll.Upper[^1]))
        {
            double width = boll.Upper[^1] - boll.Lower[^1];
            if (width > 0)
            {
                double pos = (last - boll.Lower[^1]) / width; // 0..1
                if (last > boll.Upper[^1]) { bollScore = -30; signals.Add("价格突破 BOLL 上轨，短期或回调"); }
                else if (last < boll.Lower[^1]) { bollScore = 30; signals.Add("价格跌破 BOLL 下轨，短期或反弹"); }
                else bollScore = (pos - 0.5) * 40;
            }
        }
        comps.Add(("BOLL", bollScore));

        // ---- 量能 ----
        var vols = bars.Select(b => b.Volume).ToArray();
        double volMa5 = vols.Skip(Math.Max(0, n - 5)).Average();
        double volMa20 = vols.Skip(Math.Max(0, n - 20)).Average();
        double volScore = 0;
        if (volMa20 > 0)
        {
            double ratio = volMa5 / volMa20;
            bool up = last > close[^2];
            if (ratio > 1.5 && up) { volScore = 60; signals.Add($"放量上涨 (V5/V20={ratio:F2})"); }
            else if (ratio > 1.5 && !up) { volScore = -60; signals.Add($"放量下跌 (V5/V20={ratio:F2})"); }
            else if (ratio < 0.7 && up) { volScore = -10; signals.Add($"缩量上涨，动能不足 (V5/V20={ratio:F2})"); }
            else if (ratio < 0.7 && !up) { volScore = 10; signals.Add($"缩量下跌，抛压减轻 (V5/V20={ratio:F2})"); }
            else volScore = (ratio - 1) * 30 * (up ? 1 : -1);
        }
        comps.Add(("量能", volScore));

        // ---- 筹码 ----
        double chipScore = 0;
        if (chips != null && chips.AvgCost > 0)
        {
            double diff = (last - chips.AvgCost) / chips.AvgCost * 100.0;
            if (diff > 15) { chipScore = -25; signals.Add($"价高于平均成本 {diff:F1}%，套利盘较重"); }
            else if (diff > 5) { chipScore = 10; signals.Add($"价高于平均成本 {diff:F1}%，多头持仓占优"); }
            else if (diff > -5) { chipScore = 0; signals.Add($"价格接近平均成本 ({diff:+0.0;-0.0;0}%)，多空均衡"); }
            else if (diff > -15) { chipScore = 20; signals.Add($"价低于平均成本 {-diff:F1}%，存在反弹动能"); }
            else { chipScore = 35; signals.Add($"严重低于平均成本 {-diff:F1}%，深度套牢，反弹空间大"); }

            if (chips.Concentration70 > 0 && chips.Concentration70 < 0.15)
                signals.Add($"筹码高度集中（70%集中度 {chips.Concentration70:P1}），变盘临近");
        }
        comps.Add(("筹码", chipScore));

        // ---- 加权汇总 ----
        var weights = new Dictionary<string, double>
        {
            ["趋势(均线)"] = 0.22,
            ["MACD"] = 0.18,
            ["KDJ"] = 0.12,
            ["RSI"] = 0.12,
            ["BOLL"] = 0.10,
            ["量能"] = 0.16,
            ["筹码"] = 0.10,
        };
        double total = 0, wSum = 0;
        foreach (var (name, score) in comps)
        {
            if (weights.TryGetValue(name, out var w))
            {
                total += score * w; wSum += w;
            }
        }
        double finalScore = wSum > 0 ? Math.Clamp(total / wSum, -100, 100) : 0;

        string sentiment = finalScore switch
        {
            >= 60 => "极度乐观（强烈多头）",
            >= 30 => "偏多（多头占优）",
            >= 10 => "略偏多",
            > -10 => "中性/震荡",
            > -30 => "略偏空",
            > -60 => "偏空（空头占优）",
            _ => "极度悲观（强烈空头）",
        };

        string rec = finalScore switch
        {
            >= 60 => "可考虑积极持仓，但注意短线超买回调风险，设置止盈位。",
            >= 30 => "可逢低分批介入或继续持有，关注 MACD/量能确认。",
            >= 10 => "持币观望或轻仓试探，等待趋势进一步明确。",
            > -10 => "震荡区间操作，建议观望或高抛低吸小仓位。",
            > -30 => "减仓回避，避免追高，等待明确底部信号。",
            > -60 => "建议清仓或仅保留少量底仓，控制风险。",
            _ => "强烈建议规避，等待趋势反转后再考虑介入。",
        };

        return new Report(finalScore, sentiment, rec, signals, comps);
    }

    public static string Format(Report r)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"综合评分：{r.Score:F1} / 100");
        sb.AppendLine($"市场情绪：{r.Sentiment}");
        sb.AppendLine($"操作建议：{r.Recommendation}");
        sb.AppendLine();
        sb.AppendLine("—— 各维度评分 ——");
        foreach (var (n, s) in r.Components)
            sb.AppendLine($"  {n,-12}{s,7:F1}");
        sb.AppendLine();
        sb.AppendLine("—— 触发信号 ——");
        foreach (var s in r.Signals) sb.AppendLine("  • " + s);
        return sb.ToString();
    }
}
