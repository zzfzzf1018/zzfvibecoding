using StockTechAnalyzer.Models;

namespace StockTechAnalyzer.Indicators;

/// <summary>
/// 简化版筹码分布估算（基于三角分布 + 衰减换手）。
/// 对每根 K 线，将该日成交量按三角分布摊到 [Low, High] 价位区间，
/// 然后所有历史筹码以 (1 - turnover * decay) 衰减。
/// </summary>
public static class ChipDistribution
{
    public sealed record Result(
        double[] Prices,        // 价格档位
        double[] Chips,         // 各价位筹码量
        double AvgCost,         // 平均成本
        double Concentration70, // 70% 筹码集中度（区间宽度/均价）
        double ProfitRatio);    // 当前价获利盘比例(0-1)

    public static Result Calculate(IReadOnlyList<Kline> bars, int bins = 120, double decay = 1.0,
        double? floatShares = null)
    {
        if (bars.Count == 0)
            return new Result(Array.Empty<double>(), Array.Empty<double>(), 0, 0, 0);

        double globalLow = double.MaxValue, globalHigh = double.MinValue;
        foreach (var b in bars)
        {
            if (b.Low < globalLow) globalLow = b.Low;
            if (b.High > globalHigh) globalHigh = b.High;
        }
        if (globalHigh <= globalLow) globalHigh = globalLow + 0.01;

        double step = (globalHigh - globalLow) / bins;
        var prices = new double[bins];
        for (int i = 0; i < bins; i++) prices[i] = globalLow + (i + 0.5) * step;

        var chips = new double[bins];
        // 估算流通盘（若未提供则用最大成交量*100 近似）
        double floatVol = floatShares ?? bars.Max(b => b.Volume) * 100.0;
        if (floatVol <= 0) floatVol = 1;

        foreach (var b in bars)
        {
            if (b.High <= b.Low) continue;
            int lo = Math.Clamp((int)((b.Low - globalLow) / step), 0, bins - 1);
            int hi = Math.Clamp((int)((b.High - globalLow) / step), 0, bins - 1);
            double mid = (b.Open + b.Close) / 2.0;
            int midIdx = Math.Clamp((int)((mid - globalLow) / step), lo, hi);

            // 三角权重
            double weightSum = 0;
            for (int i = lo; i <= hi; i++)
            {
                double w = i <= midIdx
                    ? (i - lo + 1.0) / (midIdx - lo + 1.0)
                    : (hi - i + 1.0) / (hi - midIdx + 1.0);
                weightSum += w;
            }
            if (weightSum <= 0) continue;

            double turnover = Math.Min(1.0, b.Volume * 100.0 / floatVol) * decay;
            // 旧筹码衰减
            for (int i = 0; i < bins; i++) chips[i] *= (1 - turnover);

            double newAdded = b.Volume; // 用成交量作量纲
            for (int i = lo; i <= hi; i++)
            {
                double w = i <= midIdx
                    ? (i - lo + 1.0) / (midIdx - lo + 1.0)
                    : (hi - i + 1.0) / (hi - midIdx + 1.0);
                chips[i] += newAdded * (w / weightSum);
            }
        }

        double total = chips.Sum();
        double avgCost = 0;
        if (total > 0)
            for (int i = 0; i < bins; i++) avgCost += prices[i] * chips[i];
        avgCost = total > 0 ? avgCost / total : 0;

        // 70% 集中度：以中位数为中心扩展，覆盖 70% 筹码的区间宽度
        double conc = 0;
        if (total > 0)
        {
            // 排序索引按筹码降序，累加直到 70%
            var idx = Enumerable.Range(0, bins).OrderByDescending(i => chips[i]).ToArray();
            double cum = 0; double target = total * 0.7;
            int minI = int.MaxValue, maxI = int.MinValue;
            foreach (var i in idx)
            {
                cum += chips[i];
                if (i < minI) minI = i;
                if (i > maxI) maxI = i;
                if (cum >= target) break;
            }
            if (avgCost > 0)
                conc = (maxI - minI + 1) * step / avgCost;
        }

        // 获利盘：以最新收盘价计，所有成本 < 当前价的筹码占比
        double cur = bars[^1].Close;
        double profit = 0;
        if (total > 0)
        {
            for (int i = 0; i < bins; i++)
                if (prices[i] <= cur) profit += chips[i];
            profit /= total;
        }

        return new Result(prices, chips, avgCost, conc, profit);
    }
}
