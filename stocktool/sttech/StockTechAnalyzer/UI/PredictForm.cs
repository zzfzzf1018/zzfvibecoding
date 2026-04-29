using ScottPlot;
using ScottPlot.WinForms;
using StockTechAnalyzer.ML;
using StockTechAnalyzer.Models;
using Color = System.Drawing.Color;
using Label = System.Windows.Forms.Label;

namespace StockTechAnalyzer.UI;

internal sealed class PredictForm : Form
{
    private readonly StockInfo _stock;
    private readonly IReadOnlyList<Kline> _bars;
    private readonly Themes.ThemeColors _theme;
    private readonly TextBox _txt;
    private readonly FormsPlot _plot = new() { Dock = DockStyle.Fill };

    public PredictForm(StockInfo stock, IReadOnlyList<Kline> bars, Themes.ThemeColors theme)
    {
        _stock = stock; _bars = bars; _theme = theme;
        Text = $"短线方向预测 — {stock.Code} {stock.Name}";
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(1100, 680);

        var top = new Panel { Dock = DockStyle.Top, Height = 40, BackColor = theme.PanelBack };
        var btn = new Button { Text = "重新训练并预测", Left = 10, Top = 7, Width = 130 };
        btn.Click += (_, _) => RunOnce();
        top.Controls.Add(btn);

        _txt = new TextBox
        {
            Dock = DockStyle.Right, Width = 460, Multiline = true, ReadOnly = true,
            ScrollBars = ScrollBars.Vertical, Font = new Font("Consolas", 10f),
        };

        Controls.Add(_plot);
        Controls.Add(_txt);
        Controls.Add(top);
        Themes.ApplyToControls(this, theme);
        Themes.ApplyToPlot(_plot.Plot, theme);
        Load += (_, _) => RunOnce();
    }

    private void RunOnce()
    {
        UseWaitCursor = true;
        try
        {
            var predictor = new PricePredictor();
            var r = predictor.Train(_bars);
            _txt.Text = PricePredictor.Format(r);
            DrawProbability(r.UpProbability, r.TestAccuracy);
        }
        finally { UseWaitCursor = false; }
    }

    private void DrawProbability(double upProb, double testAcc)
    {
        var p = _plot.Plot; p.Clear();

        // 左侧概率条 (横向条形)
        var bar = new Bar
        {
            Position = 1,
            Value = upProb * 100,
            FillColor = upProb >= 0.5 ? _theme.RedUp : _theme.GreenDown,
            Orientation = ScottPlot.Orientation.Horizontal,
            Size = 0.5,
        };
        var basePart = new Bar
        {
            Position = 1,
            Value = 100,
            FillColor = _theme.Axis.WithAlpha(0.15),
            Orientation = ScottPlot.Orientation.Horizontal,
            Size = 0.5,
        };
        p.Add.Bars(new[] { basePart });
        p.Add.Bars(new[] { bar });

        // 中线 50%
        var vl = p.Add.VerticalLine(50);
        vl.Color = _theme.Axis;
        vl.LinePattern = LinePattern.Dotted;

        var note = p.Add.Annotation(
            $"次日上涨概率：{upProb:P1}\n样本外准确率：{testAcc:P1}",
            Alignment.UpperRight);
        note.LabelFontSize = 14;
        note.LabelBackgroundColor = ScottPlot.Color.FromARGB((uint)_theme.PanelBack.ToArgb());
        note.LabelFontColor = ScottPlot.Color.FromARGB((uint)_theme.WinFore.ToArgb());

        p.Title("方向预测概率（0% = 看跌，100% = 看涨）");
        p.Axes.SetLimitsX(0, 100);
        p.Axes.SetLimitsY(0, 2);
        p.Axes.Bottom.Label.Text = "概率 (%)";
        Themes.ApplyToPlot(p, _theme);
        _plot.Refresh();
    }
}
