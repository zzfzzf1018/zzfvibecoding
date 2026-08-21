using System.Globalization;

namespace StockAnalyzer.Desktop.Infrastructure;

/// <summary>界面展示用的数值格式化。</summary>
public static class Formatting
{
    private const double Yi = 100_000_000d;
    private const double Wan = 10_000d;

    /// <summary>市值等大额金额，自动换算为「亿 / 万」。</summary>
    public static string MoneyCompact(double? value, string suffix = "")
    {
        if (value is null || double.IsNaN(value.Value))
        {
            return "--";
        }

        double abs = Math.Abs(value.Value);

        return abs switch
        {
            >= Yi => $"{value.Value / Yi:N2} 亿{suffix}",
            >= Wan => $"{value.Value / Wan:N2} 万{suffix}",
            _ => $"{value.Value:N2}{suffix}"
        };
    }

    public static string Number(double? value, int decimals = 2)
        => value is null || double.IsNaN(value.Value)
            ? "--"
            : value.Value.ToString($"N{decimals}", CultureInfo.CurrentCulture);

    public static string Multiple(double? value)
        => value is null || double.IsNaN(value.Value) || value.Value <= 0
            ? "--"
            : $"{value.Value:N2}x";

    public static string Percent(double? value, int decimals = 2)
        => value is null || double.IsNaN(value.Value)
            ? "--"
            : $"{value.Value.ToString($"N{decimals}", CultureInfo.CurrentCulture)}%";

    public static string SignedPercent(double? value, int decimals = 2)
    {
        if (value is null || double.IsNaN(value.Value))
        {
            return "--";
        }

        string sign = value.Value > 0 ? "+" : string.Empty;
        return $"{sign}{value.Value.ToString($"N{decimals}", CultureInfo.CurrentCulture)}%";
    }

    public static string Signed(double? value, int decimals = 2)
    {
        if (value is null || double.IsNaN(value.Value))
        {
            return "--";
        }

        string sign = value.Value > 0 ? "+" : string.Empty;
        return sign + value.Value.ToString($"N{decimals}", CultureInfo.CurrentCulture);
    }

    public static string Date(DateTime? value) => value?.ToString("yyyy-MM-dd") ?? "--";

    public static string DateTimeText(DateTime? value) => value?.ToString("yyyy-MM-dd HH:mm:ss") ?? "--";
}
