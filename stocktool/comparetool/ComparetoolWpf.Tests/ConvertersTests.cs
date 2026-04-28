using System.Globalization;
using System.Windows.Media;
using ComparetoolWpf.Converters;
using Xunit;

namespace ComparetoolWpf.Tests;

public class ConvertersTests
{
    [Fact]
    public void NullableDouble_FormatsWithDigits()
    {
        var c = new NullableDoubleConverter();
        Assert.Equal("", c.Convert(null, typeof(string), null, CultureInfo.InvariantCulture));
        Assert.Equal("1.50", c.Convert(1.5, typeof(string), null, CultureInfo.InvariantCulture));
        Assert.Equal("1.500", c.Convert(1.5, typeof(string), "3", CultureInfo.InvariantCulture));
        Assert.Equal("abc", c.Convert("abc", typeof(string), null, CultureInfo.InvariantCulture));
        Assert.Throws<NotSupportedException>(() => c.ConvertBack(null, typeof(double?), null, CultureInfo.InvariantCulture));
    }

    [Fact]
    public void Percent_FormatsAsPercent()
    {
        var c = new PercentConverter();
        Assert.Equal("", c.Convert(null, typeof(string), null, CultureInfo.InvariantCulture));
        Assert.Equal("12.34%", c.Convert(0.1234, typeof(string), null, CultureInfo.InvariantCulture));
        Assert.Equal("x", c.Convert("x", typeof(string), null, CultureInfo.InvariantCulture));
        Assert.Throws<NotSupportedException>(() => c.ConvertBack(null, typeof(double?), null, CultureInfo.InvariantCulture));
    }

    [Fact]
    public void HighlightBackground_TrueGivesRed()
    {
        var c = new HighlightBackgroundConverter();
        Assert.Equal(Brushes.Transparent, c.Convert(false, typeof(Brush), null, CultureInfo.InvariantCulture));
        Assert.Equal(Brushes.Transparent, c.Convert(null, typeof(Brush), null, CultureInfo.InvariantCulture));
        var brush = (SolidColorBrush)c.Convert(true, typeof(Brush), null, CultureInfo.InvariantCulture);
        Assert.Equal(0xFF, brush.Color.R);
        Assert.Throws<NotSupportedException>(() => c.ConvertBack(null, typeof(bool), null, CultureInfo.InvariantCulture));
    }

    [Fact]
    public void ChangeBackground_PositiveAndNegative()
    {
        var c = new ChangeBackgroundConverter();
        var threshold = 20.0; // %
        var pos = (SolidColorBrush)c.Convert(new object[] { (double?)0.3, threshold }, typeof(Brush), null, CultureInfo.InvariantCulture);
        var neg = (SolidColorBrush)c.Convert(new object[] { (double?)-0.3, threshold }, typeof(Brush), null, CultureInfo.InvariantCulture);
        var none = c.Convert(new object[] { (double?)0.05, threshold }, typeof(Brush), null, CultureInfo.InvariantCulture);
        var nullVal = c.Convert(new object[] { null!, threshold }, typeof(Brush), null, CultureInfo.InvariantCulture);
        Assert.True(pos.Color.G > pos.Color.R);              // 浅绿
        Assert.True(neg.Color.R > neg.Color.G);              // 浅红
        Assert.Equal(Brushes.Transparent, none);
        Assert.Equal(Brushes.Transparent, nullVal);
    }

    [Fact]
    public void ChangeBackground_StringThreshold()
    {
        var c = new ChangeBackgroundConverter();
        var brush = c.Convert(new object[] { (double?)0.5, "10" }, typeof(Brush), null, CultureInfo.InvariantCulture);
        Assert.IsType<SolidColorBrush>(brush);
    }

    [Fact]
    public void ChangeBackground_TooFewArgs()
    {
        var c = new ChangeBackgroundConverter();
        Assert.Equal(Brushes.Transparent, c.Convert(new object[] { 1.0 }, typeof(Brush), null, CultureInfo.InvariantCulture));
        Assert.Throws<NotSupportedException>(() =>
            c.ConvertBack(null!, new[] { typeof(double) }, null, CultureInfo.InvariantCulture));
    }
}
