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
