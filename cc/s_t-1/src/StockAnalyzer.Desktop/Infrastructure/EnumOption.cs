namespace StockAnalyzer.Desktop.Infrastructure;

/// <summary>用于 ComboBox 绑定的枚举选项包装。</summary>
public sealed class EnumOption<T> where T : struct, Enum
{
    public EnumOption(T value, string display)
    {
        Value = value;
        Display = display;
    }

    public T Value { get; }

    public string Display { get; }

    public override string ToString() => Display;
}

/// <summary>基本信息面板的一个「标签 - 值」条目。</summary>
public sealed class MetricItem
{
    public MetricItem(string label, string value, double? trend = null, string? tooltip = null)
    {
        Label = label;
        Value = value;
        Trend = trend;
        Tooltip = tooltip;
    }

    public string Label { get; }

    public string Value { get; }

    /// <summary>用于着色的趋势值（正红负绿）；为 null 时使用主文本色。</summary>
    public double? Trend { get; }

    public string? Tooltip { get; }
}
