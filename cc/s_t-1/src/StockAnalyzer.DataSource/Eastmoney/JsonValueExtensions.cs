using System.Globalization;
using System.Text.Json;

namespace StockAnalyzer.DataSource.Eastmoney;

/// <summary>东财接口返回值的容错解析工具（缺失值常表现为 "-" 或 0）。</summary>
internal static class JsonValueExtensions
{
    public static double? GetDoubleOrNull(this JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out JsonElement value))
        {
            return null;
        }

        return ToDouble(value);
    }

    public static double? ToDouble(this JsonElement value)
    {
        switch (value.ValueKind)
        {
            case JsonValueKind.Number:
                return value.TryGetDouble(out double number) ? number : null;

            case JsonValueKind.String:
                string? text = value.GetString();
                if (string.IsNullOrWhiteSpace(text) || text == "-" || text == "--")
                {
                    return null;
                }

                return double.TryParse(text, NumberStyles.Any, CultureInfo.InvariantCulture, out double parsed)
                    ? parsed
                    : null;

            default:
                return null;
        }
    }

    public static string? GetStringOrNull(this JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out JsonElement value))
        {
            return null;
        }

        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString(),
            JsonValueKind.Number => value.ToString(),
            _ => null
        };
    }

    public static int? GetInt32OrNull(this JsonElement element, string propertyName)
    {
        double? value = element.GetDoubleOrNull(propertyName);
        return value.HasValue ? (int)value.Value : null;
    }

    public static DateTime? GetDateOrNull(this JsonElement element, string propertyName)
    {
        string? text = element.GetStringOrNull(propertyName);
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        return DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime date)
            ? date.Date
            : null;
    }

    /// <summary>把 0 视为缺失（东财对无意义指标常返回 0）。</summary>
    public static double? NullIfZero(this double? value) =>
        value is null || Math.Abs(value.Value) < double.Epsilon ? null : value;
}
