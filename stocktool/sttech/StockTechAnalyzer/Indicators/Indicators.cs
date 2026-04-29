using StockTechAnalyzer.Models;

namespace StockTechAnalyzer.Indicators;

/// <summary>
/// 常用技术指标计算（输入按时间升序）。
/// </summary>
public static class Indicators
{
    // ---- 简单/指数移动平均 -----------------------------------------------
    public static double[] SMA(IReadOnlyList<double> src, int period)
    {
        var n = src.Count;
        var r = new double[n];
        double sum = 0;
        for (int i = 0; i < n; i++)
        {
            sum += src[i];
            if (i >= period) sum -= src[i - period];
            r[i] = i >= period - 1 ? sum / period : double.NaN;
        }
        return r;
    }

    public static double[] EMA(IReadOnlyList<double> src, int period)
    {
        var n = src.Count;
        var r = new double[n];
        if (n == 0) return r;
        double k = 2.0 / (period + 1);
        r[0] = src[0];
        for (int i = 1; i < n; i++)
            r[i] = src[i] * k + r[i - 1] * (1 - k);
        return r;
    }

    // ---- MACD ------------------------------------------------------------
    public sealed record MacdResult(double[] Dif, double[] Dea, double[] Hist);

    public static MacdResult MACD(IReadOnlyList<double> close, int fast = 12, int slow = 26, int signal = 9)
    {
        var emaFast = EMA(close, fast);
        var emaSlow = EMA(close, slow);
        int n = close.Count;
        var dif = new double[n];
        for (int i = 0; i < n; i++) dif[i] = emaFast[i] - emaSlow[i];
        var dea = EMA(dif, signal);
        var hist = new double[n];
        for (int i = 0; i < n; i++) hist[i] = (dif[i] - dea[i]) * 2.0;
        return new MacdResult(dif, dea, hist);
    }

    // ---- KDJ -------------------------------------------------------------
    public sealed record KdjResult(double[] K, double[] D, double[] J);

    public static KdjResult KDJ(IReadOnlyList<Kline> bars, int n = 9, int m1 = 3, int m2 = 3)
    {
        int len = bars.Count;
        var k = new double[len];
        var d = new double[len];
        var j = new double[len];
        double prevK = 50, prevD = 50;
        for (int i = 0; i < len; i++)
        {
            int start = Math.Max(0, i - n + 1);
            double hh = double.MinValue, ll = double.MaxValue;
            for (int t = start; t <= i; t++)
            {
                if (bars[t].High > hh) hh = bars[t].High;
                if (bars[t].Low < ll) ll = bars[t].Low;
            }
            double rsv = hh == ll ? 0 : (bars[i].Close - ll) / (hh - ll) * 100.0;
            double curK = (2.0 / m1) * prevK + (1.0 / m1) * rsv;
            double curD = (2.0 / m2) * prevD + (1.0 / m2) * curK;
            double curJ = 3 * curK - 2 * curD;
            k[i] = curK; d[i] = curD; j[i] = curJ;
            prevK = curK; prevD = curD;
        }
        return new KdjResult(k, d, j);
    }

    // ---- RSI -------------------------------------------------------------
    public static double[] RSI(IReadOnlyList<double> close, int period = 14)
    {
        int n = close.Count;
        var r = new double[n];
        if (n < 2) return r;
        double avgGain = 0, avgLoss = 0;
        for (int i = 1; i < n; i++)
        {
            double diff = close[i] - close[i - 1];
            double gain = diff > 0 ? diff : 0;
            double loss = diff < 0 ? -diff : 0;
            if (i <= period)
            {
                avgGain += gain; avgLoss += loss;
                if (i == period)
                {
                    avgGain /= period; avgLoss /= period;
                    r[i] = avgLoss == 0 ? 100 : 100 - 100 / (1 + avgGain / avgLoss);
                }
                else r[i] = double.NaN;
            }
            else
            {
                avgGain = (avgGain * (period - 1) + gain) / period;
                avgLoss = (avgLoss * (period - 1) + loss) / period;
                r[i] = avgLoss == 0 ? 100 : 100 - 100 / (1 + avgGain / avgLoss);
            }
        }
        r[0] = double.NaN;
        return r;
    }

    // ---- BOLL ------------------------------------------------------------
    public sealed record BollResult(double[] Mid, double[] Upper, double[] Lower);

    public static BollResult BOLL(IReadOnlyList<double> close, int period = 20, double k = 2.0)
    {
        int n = close.Count;
        var mid = SMA(close, period);
        var up = new double[n];
        var lo = new double[n];
        for (int i = 0; i < n; i++)
        {
            if (i < period - 1) { up[i] = double.NaN; lo[i] = double.NaN; continue; }
            double sum = 0;
            for (int t = i - period + 1; t <= i; t++)
            {
                double dv = close[t] - mid[i];
                sum += dv * dv;
            }
            double sd = Math.Sqrt(sum / period);
            up[i] = mid[i] + k * sd;
            lo[i] = mid[i] - k * sd;
        }
        return new BollResult(mid, up, lo);
    }
}
