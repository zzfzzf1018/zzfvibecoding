using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using StockAnalyzer.Core.Models;

namespace StockAnalyzer.Desktop.Infrastructure;

/// <summary>正数红、负数绿（A 股习惯），零值为次要色。</summary>
public sealed class ChangeToBrushConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        // 可空 double 装箱后就是 double，无需单独匹配
        double? number = value is double d ? d : null;

        string key = number switch
        {
            null => "TextSecondaryBrush",
            > 0 => "UpBrush",
            < 0 => "DownBrush",
            _ => "TextPrimaryBrush"
        };

        return Application.Current.TryFindResource(key) as Brush ?? Brushes.Gray;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>把百分位（0~100）映射为「低估绿 → 高估红」的颜色。</summary>
public sealed class PercentileToBrushConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not double percentile || double.IsNaN(percentile))
        {
            return Application.Current.TryFindResource("TextSecondaryBrush") as Brush ?? Brushes.Gray;
        }

        return percentile switch
        {
            < 30 => new SolidColorBrush(Color.FromRgb(0x2F, 0xBF, 0x71)),
            < 70 => new SolidColorBrush(Color.FromRgb(0xE0, 0xB2, 0x4A)),
            _ => new SolidColorBrush(Color.FromRgb(0xF0, 0x5C, 0x5C))
        };
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>bool → Visibility，支持 Invert 参数。</summary>
public sealed class BooleanToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        bool flag = value is true;
        bool invert = string.Equals(parameter as string, "Invert", StringComparison.OrdinalIgnoreCase);
        return flag ^ invert ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>集合元素个数 → Visibility；参数 Invert 时表示「为空才显示」。</summary>
public sealed class CountToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        int count = value is int number ? number : 0;
        bool invert = string.Equals(parameter as string, "Invert", StringComparison.OrdinalIgnoreCase);
        return count > 0 ^ invert ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>市场枚举 → 中文名称。</summary>
public sealed class MarketNameConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is MarketType market ? market.ToDisplayName() : "--";

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
