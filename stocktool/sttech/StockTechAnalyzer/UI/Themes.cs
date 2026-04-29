using ScottPlot;
using ScottPlot.WinForms;
using Color = System.Drawing.Color;
using Label = System.Windows.Forms.Label;

namespace StockTechAnalyzer.UI;

/// <summary>
/// 主题（浅色 / 深色）应用工具。
/// </summary>
internal static class Themes
{
    public sealed record ThemeColors(
        Color WinBack, Color WinFore, Color PanelBack, Color HeaderBack,
        ScottPlot.Color FigBack, ScottPlot.Color DataBack, ScottPlot.Color Axis, ScottPlot.Color Grid,
        ScottPlot.Color RedUp, ScottPlot.Color GreenDown);

    public static readonly ThemeColors Light = new(
        WinBack: Color.White,
        WinFore: Color.Black,
        PanelBack: Color.FromArgb(245, 245, 248),
        HeaderBack: Color.Gainsboro,
        FigBack: ScottPlot.Color.FromHex("#FFFFFF"),
        DataBack: ScottPlot.Color.FromHex("#FBFBFB"),
        Axis: ScottPlot.Color.FromHex("#222222"),
        Grid: ScottPlot.Color.FromHex("#E5E5E5"),
        RedUp: ScottPlot.Color.FromHex("#E03131"),
        GreenDown: ScottPlot.Color.FromHex("#2F9E44"));

    public static readonly ThemeColors Dark = new(
        WinBack: Color.FromArgb(24, 26, 32),
        WinFore: Color.FromArgb(220, 220, 220),
        PanelBack: Color.FromArgb(34, 36, 44),
        HeaderBack: Color.FromArgb(46, 48, 56),
        FigBack: ScottPlot.Color.FromHex("#181A20"),
        DataBack: ScottPlot.Color.FromHex("#22242C"),
        Axis: ScottPlot.Color.FromHex("#CCCCCC"),
        Grid: ScottPlot.Color.FromHex("#3A3D45"),
        RedUp: ScottPlot.Color.FromHex("#FF5252"),
        GreenDown: ScottPlot.Color.FromHex("#00C853"));

    public static void ApplyToPlot(Plot plot, ThemeColors c)
    {
        plot.FigureBackground.Color = c.FigBack;
        plot.DataBackground.Color = c.DataBack;
        plot.Axes.Color(c.Axis);
        plot.Grid.MajorLineColor = c.Grid;
    }

    /// <summary>递归把 WinForms 控件树染色。</summary>
    public static void ApplyToControls(Control root, ThemeColors c)
    {
        ApplyOne(root, c);
        foreach (Control child in root.Controls)
            ApplyToControls(child, c);
    }

    private static void ApplyOne(Control ctl, ThemeColors c)
    {
        switch (ctl)
        {
            case FormsPlot:
                return; // 由 ApplyToPlot 处理
            case Button b:
                b.BackColor = c.PanelBack;
                b.ForeColor = c.WinFore;
                b.FlatStyle = FlatStyle.Flat;
                b.FlatAppearance.BorderColor = c.HeaderBack;
                break;
            case TextBox t:
                t.BackColor = c.WinBack == Color.White ? Color.White : Color.FromArgb(40, 42, 50);
                t.ForeColor = c.WinFore;
                t.BorderStyle = BorderStyle.FixedSingle;
                break;
            case ListBox lb:
                lb.BackColor = c.WinBack;
                lb.ForeColor = c.WinFore;
                break;
            case TabControl:
                ctl.BackColor = c.PanelBack;
                ctl.ForeColor = c.WinFore;
                break;
            case TabPage tp:
                tp.BackColor = c.WinBack;
                tp.ForeColor = c.WinFore;
                break;
            case Label lab:
                if (lab.BackColor == Color.Gainsboro || lab.BackColor == SystemColors.Control)
                    lab.BackColor = c.HeaderBack;
                lab.ForeColor = c.WinFore;
                break;
            case Panel p:
                if (p.BackColor == Color.Gainsboro || p.BackColor == Color.FromArgb(245, 245, 248) || p.BackColor == SystemColors.Control)
                    p.BackColor = c.PanelBack;
                p.ForeColor = c.WinFore;
                break;
            case Form f:
                f.BackColor = c.WinBack;
                f.ForeColor = c.WinFore;
                break;
            default:
                ctl.BackColor = c.WinBack;
                ctl.ForeColor = c.WinFore;
                break;
        }
    }
}
