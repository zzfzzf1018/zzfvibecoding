namespace StockAnalyzer.Core.Analytics;

/// <summary>分位数 / 百分位计算工具。</summary>
public static class PercentileCalculator
{
    /// <summary>
    /// 计算 <paramref name="value"/> 在样本中的百分位（0~100）。
    /// 采用 "小于等于计数法"：rank = count(x &lt;= value) / n，与东财、理杏仁等平台口径一致。
    /// </summary>
    /// <param name="sortedAscending">已升序排序的样本。</param>
    public static double PercentRank(IReadOnlyList<double> sortedAscending, double value)
    {
        if (sortedAscending.Count == 0)
        {
            return double.NaN;
        }

        int count = UpperBound(sortedAscending, value);
        return count * 100.0 / sortedAscending.Count;
    }

    /// <summary>
    /// 线性插值分位数（等价于 Excel PERCENTILE.INC / numpy 默认 linear 方法）。
    /// </summary>
    /// <param name="sortedAscending">已升序排序的样本。</param>
    /// <param name="quantile">分位，0~1。</param>
    public static double Quantile(IReadOnlyList<double> sortedAscending, double quantile)
    {
        if (sortedAscending.Count == 0)
        {
            return double.NaN;
        }

        if (sortedAscending.Count == 1)
        {
            return sortedAscending[0];
        }

        quantile = Math.Clamp(quantile, 0.0, 1.0);
        double position = quantile * (sortedAscending.Count - 1);
        int lower = (int)Math.Floor(position);
        int upper = (int)Math.Ceiling(position);

        if (lower == upper)
        {
            return sortedAscending[lower];
        }

        double weight = position - lower;
        return sortedAscending[lower] * (1 - weight) + sortedAscending[upper] * weight;
    }

    public static double Median(IReadOnlyList<double> sortedAscending) => Quantile(sortedAscending, 0.5);

    /// <summary>返回第一个大于 <paramref name="value"/> 的下标，即 &lt;= value 的元素个数。</summary>
    private static int UpperBound(IReadOnlyList<double> sortedAscending, double value)
    {
        int low = 0;
        int high = sortedAscending.Count;

        while (low < high)
        {
            int mid = low + ((high - low) >> 1);
            if (sortedAscending[mid] <= value)
            {
                low = mid + 1;
            }
            else
            {
                high = mid;
            }
        }

        return low;
    }
}
