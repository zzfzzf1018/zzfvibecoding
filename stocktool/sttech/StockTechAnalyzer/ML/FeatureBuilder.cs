using StockTechAnalyzer.Indicators;
using StockTechAnalyzer.Models;

namespace StockTechAnalyzer.ML;

/// <summary>
/// 把 K 线 + 技术指标转换成可用于机器学习的样本：
/// X = [收益滞后, MA 斜率, MACD Hist, KDJ J, RSI, BOLL 位置, 量比变化]
/// y = 次日是否上涨 (1/0)
/// </summary>
internal static class FeatureBuilder
{
    public const int FeatureCount = 9;

    public static (double[][] X, int[] y, string[] FeatureNames) Build(IReadOnlyList<Kline> bars)
    {
        if (bars.Count < 80)
            return (Array.Empty<double[]>(), Array.Empty<int>(), FeatureNames());

        var closes = bars.Select(b => b.Close).ToArray();
        var vols = bars.Select(b => b.Volume).ToArray();
        var ma5 = Indicators.Indicators.SMA(closes, 5);
        var ma10 = Indicators.Indicators.SMA(closes, 10);
        var ma20 = Indicators.Indicators.SMA(closes, 20);
        var macd = Indicators.Indicators.MACD(closes);
        var kdj = Indicators.Indicators.KDJ(bars);
        var rsi = Indicators.Indicators.RSI(closes);
        var boll = Indicators.Indicators.BOLL(closes);

        var xs = new List<double[]>();
        var ys = new List<int>();
        for (int i = 30; i < bars.Count - 1; i++)
        {
            if (double.IsNaN(ma20[i]) || double.IsNaN(boll.Mid[i])) continue;
            double r1 = SafeRet(closes, i, 1);
            double r3 = SafeRet(closes, i, 3);
            double r5 = SafeRet(closes, i, 5);
            double maSlope = (ma5[i] - ma5[i - 3]) / Math.Max(1e-9, ma5[i - 3]);
            double maDiff = (ma5[i] - ma20[i]) / Math.Max(1e-9, ma20[i]);
            double bollPos = (closes[i] - boll.Lower[i]) / Math.Max(1e-9, boll.Upper[i] - boll.Lower[i]);
            double volChg = vols[i - 1] > 0 ? (vols[i] / vols[i - 1] - 1.0) : 0;

            xs.Add(new[]
            {
                r1, r3, r5,
                maSlope, maDiff,
                macd.Hist[i],
                Clamp(kdj.J[i], -50, 150) / 100.0,
                Clamp(rsi[i], 0, 100) / 100.0 - 0.5,
                Clamp(bollPos, -0.5, 1.5),
                Math.Tanh(volChg),
            }.Take(FeatureCount).ToArray());
            ys.Add(closes[i + 1] > closes[i] ? 1 : 0);
        }
        return (xs.ToArray(), ys.ToArray(), FeatureNames());
    }

    /// <summary>构造"最新一根 K 线"的特征（用于预测下一交易日方向）。</summary>
    public static double[]? BuildLatest(IReadOnlyList<Kline> bars)
    {
        if (bars.Count < 30) return null;
        var closes = bars.Select(b => b.Close).ToArray();
        var vols = bars.Select(b => b.Volume).ToArray();
        var ma5 = Indicators.Indicators.SMA(closes, 5);
        var ma20 = Indicators.Indicators.SMA(closes, 20);
        var macd = Indicators.Indicators.MACD(closes);
        var kdj = Indicators.Indicators.KDJ(bars);
        var rsi = Indicators.Indicators.RSI(closes);
        var boll = Indicators.Indicators.BOLL(closes);

        int i = bars.Count - 1;
        if (double.IsNaN(ma20[i]) || double.IsNaN(boll.Mid[i])) return null;

        double r1 = SafeRet(closes, i, 1);
        double r3 = SafeRet(closes, i, 3);
        double r5 = SafeRet(closes, i, 5);
        double maSlope = (ma5[i] - ma5[i - 3]) / Math.Max(1e-9, ma5[i - 3]);
        double maDiff = (ma5[i] - ma20[i]) / Math.Max(1e-9, ma20[i]);
        double bollPos = (closes[i] - boll.Lower[i]) / Math.Max(1e-9, boll.Upper[i] - boll.Lower[i]);
        double volChg = vols[i - 1] > 0 ? (vols[i] / vols[i - 1] - 1.0) : 0;
        return new[]
        {
            r1, r3, r5,
            maSlope, maDiff,
            macd.Hist[i],
            Clamp(kdj.J[i], -50, 150) / 100.0,
            Clamp(rsi[i], 0, 100) / 100.0 - 0.5,
            Clamp(bollPos, -0.5, 1.5),
            Math.Tanh(volChg),
        }.Take(FeatureCount).ToArray();
    }

    public static string[] FeatureNames() => new[]
    {
        "1日收益率", "3日收益率", "5日收益率",
        "MA5斜率", "MA5/MA20偏离",
        "MACD柱", "KDJ-J(归一)", "RSI(中心化)", "BOLL位置",
    };

    private static double SafeRet(double[] c, int i, int n)
        => i - n < 0 || c[i - n] <= 0 ? 0 : c[i] / c[i - n] - 1.0;

    private static double Clamp(double v, double lo, double hi)
        => double.IsNaN(v) ? 0 : Math.Max(lo, Math.Min(hi, v));
}
