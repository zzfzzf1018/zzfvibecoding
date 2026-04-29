using System.Text;
using StockTechAnalyzer.Models;

namespace StockTechAnalyzer.Indicators;

/// <summary>
/// 风险指标：年化波动率 / 最大回撤 / ATR / 夏普比率（rf=0）。
/// </summary>
public static class RiskMetrics
{
    public sealed record Result(
        double AnnualVolatility,    // 年化波动率（小数, 0.25=25%）
        double MaxDrawdown,         // 最大回撤（负值，-0.30=-30%）
        double MaxDrawdownDays,     // 最大回撤恢复时长（K 线数，未恢复给当前长度）
        double Atr14,               // ATR(14)
        double Sharpe,              // 年化夏普
        double AvgDailyReturn,      // 区间日均收益率
        double TotalReturn);        // 区间总收益率

    public static Result Calculate(IReadOnlyList<Kline> bars, int periodsPerYear = 252)
    {
        if (bars.Count < 2)
            return new Result(0, 0, 0, 0, 0, 0, 0);

        // 收益率
        var rets = new double[bars.Count - 1];
        for (int i = 1; i < bars.Count; i++)
        {
            rets[i - 1] = bars[i].Close / bars[i - 1].Close - 1.0;
        }
        double mean = rets.Average();
        double variance = rets.Sum(r => (r - mean) * (r - mean)) / Math.Max(1, rets.Length - 1);
        double std = Math.Sqrt(variance);
        double annVol = std * Math.Sqrt(periodsPerYear);
        double sharpe = std > 0 ? mean / std * Math.Sqrt(periodsPerYear) : 0;

        // 最大回撤
        double peak = bars[0].Close;
        int peakIdx = 0;
        double maxDd = 0;
        int maxDdLen = 0;
        for (int i = 0; i < bars.Count; i++)
        {
            if (bars[i].Close > peak) { peak = bars[i].Close; peakIdx = i; }
            double dd = bars[i].Close / peak - 1.0;
            if (dd < maxDd)
            {
                maxDd = dd;
                maxDdLen = i - peakIdx;
            }
        }

        // ATR(14)
        double atr = 0;
        if (bars.Count >= 15)
        {
            double sumTr = 0;
            for (int i = 1; i <= 14; i++)
            {
                double tr = TrueRange(bars[i - 1], bars[i]);
                sumTr += tr;
            }
            atr = sumTr / 14;
            for (int i = 15; i < bars.Count; i++)
            {
                double tr = TrueRange(bars[i - 1], bars[i]);
                atr = (atr * 13 + tr) / 14;
            }
        }

        double total = bars[^1].Close / bars[0].Close - 1.0;
        return new Result(annVol, maxDd, maxDdLen, atr, sharpe, mean, total);
    }

    private static double TrueRange(Kline prev, Kline cur)
    {
        double a = cur.High - cur.Low;
        double b = Math.Abs(cur.High - prev.Close);
        double c = Math.Abs(cur.Low - prev.Close);
        return Math.Max(a, Math.Max(b, c));
    }

    public static string Format(Result r, double currentPrice)
    {
        var sb = new StringBuilder();
        sb.AppendLine("════════════ 风险指标 ════════════");
        sb.AppendLine();
        sb.AppendLine($"  · 区间总收益       {r.TotalReturn:P2}");
        sb.AppendLine($"  · 日均收益率       {r.AvgDailyReturn:P3}");
        sb.AppendLine($"  · 年化波动率       {r.AnnualVolatility:P2}   {VolDesc(r.AnnualVolatility)}");
        sb.AppendLine($"  · 年化夏普比率     {r.Sharpe:F2}        {SharpeDesc(r.Sharpe)}");
        sb.AppendLine($"  · 最大回撤         {r.MaxDrawdown:P2}   持续 {r.MaxDrawdownDays:F0} 根K线");
        sb.AppendLine($"  · ATR(14) 真实波幅 {r.Atr14:F3}");
        sb.AppendLine();
        sb.AppendLine("—— ATR 止损建议 ——");
        sb.AppendLine($"  · 现价 {currentPrice:F2}");
        sb.AppendLine($"  · 1×ATR 止损位     {currentPrice - r.Atr14:F2}");
        sb.AppendLine($"  · 2×ATR 止损位     {currentPrice - 2 * r.Atr14:F2}（推荐，吸收正常波动）");
        sb.AppendLine($"  · 3×ATR 止损位     {currentPrice - 3 * r.Atr14:F2}（宽松，适合长线）");
        sb.AppendLine();
        sb.AppendLine("提示：年化波动率 < 25% 为低波动；25-50% 中等；> 50% 偏高。");
        sb.AppendLine("夏普 > 1 为优秀，0.5-1 一般，< 0 表示承担风险却亏损。");
        return sb.ToString();
    }

    private static string VolDesc(double v) => v switch
    {
        < 0.15 => "（低波动）",
        < 0.25 => "（较低）",
        < 0.40 => "（中等）",
        < 0.60 => "（偏高）",
        _ => "（高波动，风险大）",
    };

    private static string SharpeDesc(double s) => s switch
    {
        > 2 => "（卓越）",
        > 1 => "（优秀）",
        > 0.5 => "（良好）",
        > 0 => "（一般）",
        _ => "（亏损）",
    };
}
