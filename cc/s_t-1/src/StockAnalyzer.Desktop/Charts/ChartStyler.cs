using ScottPlot;

namespace StockAnalyzer.Desktop.Charts;

/// <summary>统一图表配色，保持与深色主题一致。</summary>
internal static class ChartStyler
{
    public static readonly Color Background = Color.FromHex("#1A2029");
    public static readonly Color Grid = Color.FromHex("#2E3947");
    public static readonly Color Axis = Color.FromHex("#93A1B3");
    public static readonly Color Price = Color.FromHex("#E6EAF0");

    /// <summary>由低到高的分位带配色：绿(低估) → 黄 → 红(高估)。</summary>
    public static readonly Color[] BandPalette =
    {
        Color.FromHex("#2FBF71"),
        Color.FromHex("#7FC24E"),
        Color.FromHex("#E0B24A"),
        Color.FromHex("#EF8A4A"),
        Color.FromHex("#F05C5C")
    };

    public static void ApplyDarkTheme(Plot plot)
    {
        plot.FigureBackground.Color = Background;
        plot.DataBackground.Color = Background;
        plot.Axes.Color(Axis);
        plot.Grid.MajorLineColor = Grid;
        plot.Legend.BackgroundColor = Color.FromHex("#222A35");
        plot.Legend.FontColor = Color.FromHex("#E6EAF0");
        plot.Legend.OutlineColor = Grid;
    }

    /// <summary>
    /// 在所有图元添加完成后调用：让 ScottPlot 根据图中实际出现的字符自动挑选支持中文的字体。
    /// 直接指定字体名在部分系统上会静默回退到不含中文字形的默认字体，因此优先用自动探测。
    /// </summary>
    /// <summary>
    /// 在所有图元添加完成后调用，让图表正确显示中文。
    /// </summary>
    /// <remarks>
    /// 不能直接 <c>plot.Font.Set("Microsoft YaHei")</c>：SkiaSharp 在找不到该字体时会静默回退到
    /// 不含中文字形的字体，反而把 <see cref="FontStyler.Automatic"/> 已经选对的字体覆盖掉。
    /// 因此统一使用自动探测结果；标题与坐标轴标签不在 Automatic 的覆盖范围内，需要单独补齐。
    /// </remarks>
    public static void ApplyChineseFont(Plot plot)
    {
        plot.Font.Automatic();

        string font = Fonts.Detect("市盈率估值通道");
        plot.Axes.Title.Label.FontName = font;
        plot.Axes.Left.Label.FontName = font;
        plot.Axes.Bottom.Label.FontName = font;
    }

    public static Color BandColor(int index, int total)
    {
        if (total <= 1)
        {
            return BandPalette[^1];
        }

        double ratio = (double)index / (total - 1);
        int paletteIndex = (int)Math.Round(ratio * (BandPalette.Length - 1));
        return BandPalette[Math.Clamp(paletteIndex, 0, BandPalette.Length - 1)];
    }
}
