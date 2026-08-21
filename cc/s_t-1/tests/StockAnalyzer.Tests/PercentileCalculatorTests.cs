using StockAnalyzer.Core.Analytics;
using Xunit;

namespace StockAnalyzer.Tests;

public class PercentileCalculatorTests
{
    private static readonly double[] Sample = { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };

    [Fact]
    public void PercentRank_UsesLessThanOrEqualCounting()
    {
        Assert.Equal(10, PercentileCalculator.PercentRank(Sample, 1));
        Assert.Equal(50, PercentileCalculator.PercentRank(Sample, 5));
        Assert.Equal(100, PercentileCalculator.PercentRank(Sample, 10));
    }

    [Fact]
    public void PercentRank_ClampsOutOfRangeValues()
    {
        Assert.Equal(0, PercentileCalculator.PercentRank(Sample, 0.5));
        Assert.Equal(100, PercentileCalculator.PercentRank(Sample, 99));
    }

    [Fact]
    public void PercentRank_CountsDuplicates()
    {
        double[] withDuplicates = { 1, 5, 5, 5, 9 };
        Assert.Equal(80, PercentileCalculator.PercentRank(withDuplicates, 5));
    }

    [Fact]
    public void Quantile_MatchesLinearInterpolation()
    {
        // 与 Excel PERCENTILE.INC / numpy 默认 linear 口径一致
        Assert.Equal(1, PercentileCalculator.Quantile(Sample, 0));
        Assert.Equal(10, PercentileCalculator.Quantile(Sample, 1));
        Assert.Equal(5.5, PercentileCalculator.Quantile(Sample, 0.5), 6);
        Assert.Equal(1.9, PercentileCalculator.Quantile(Sample, 0.1), 6);
    }

    [Fact]
    public void EmptySample_ReturnsNaN()
    {
        Assert.True(double.IsNaN(PercentileCalculator.PercentRank(Array.Empty<double>(), 1)));
        Assert.True(double.IsNaN(PercentileCalculator.Quantile(Array.Empty<double>(), 0.5)));
    }
}
