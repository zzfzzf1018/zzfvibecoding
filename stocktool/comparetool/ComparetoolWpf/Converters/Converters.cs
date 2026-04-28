using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace ComparetoolWpf.Converters;

/// <summary>
/// 把 double? 数值按千分位与可选小数位格式化。
/// 参数：小数位（默认 2）。
/// </summary>
public class NullableDoubleConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is null) return string.Empty;
        if (value is double d)
        {
            int digits = 2;
            if (parameter is string sp && int.TryParse(sp, out var n)) digits = n;
            return d.ToString("N" + digits, CultureInfo.InvariantCulture);
        }
        return value.ToString() ?? string.Empty;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>把 double?（0.123）渲染成百分数字符串 "12.30%"。</summary>
public class PercentConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is null) return string.Empty;
        if (value is double d) return (d * 100).ToString("F2", CultureInfo.InvariantCulture) + "%";
        return value.ToString() ?? string.Empty;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>bool -> 高亮背景色（true = 浅红色，false = 透明）。</summary>
public class HighlightBackgroundConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool b && b)
            return new SolidColorBrush(Color.FromRgb(0xFF, 0xE0, 0xE0));
        return Brushes.Transparent;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>
/// 根据变化率 + 阈值返回背景色：
///   |change| 超过阈值且为正 -> 浅绿；
///   |change| 超过阈值且为负 -> 浅红；
///   否则透明。
/// 通过 MultiBinding 传入 (ChangeRatio, ThresholdPercent)。
/// </summary>
public class ChangeBackgroundConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object? parameter, CultureInfo culture)
    {
        if (values.Length < 2) return Brushes.Transparent;
        double? change = values[0] as double?;
        double threshold = 0.2;
        if (values[1] is double t) threshold = t / 100.0;
        else if (values[1] is string s && double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var ts))
            threshold = ts / 100.0;
        if (!change.HasValue) return Brushes.Transparent;
        if (Math.Abs(change.Value) < threshold) return Brushes.Transparent;
        return change.Value >= 0
            ? new SolidColorBrush(Color.FromRgb(0xDC, 0xF5, 0xDC))   // 浅绿
            : new SolidColorBrush(Color.FromRgb(0xFF, 0xDC, 0xDC));  // 浅红
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
