using ScottPlot;
using ScottPlot.WinForms;
using StockTechAnalyzer.Models;
using StockTechAnalyzer.Services;
using Color = System.Drawing.Color;
using Label = System.Windows.Forms.Label;

namespace StockTechAnalyzer.UI;

/// <summary>
/// 个股对比 / 大盘联动：多只股票（含指数）按起始日归一化叠加。
/// </summary>
internal sealed class CompareForm : Form
{
    private readonly IStockDataSource _src;
    private readonly SinaStockDataSource _sina;
    private readonly Themes.ThemeColors _theme;
    private readonly TextBox _txtCodes;
    private readonly NumericUpDown _numDays;
    private readonly FormsPlot _plot = new() { Dock = DockStyle.Fill };

    private static readonly string[] Palette =
        { "#E03131", "#1971C2", "#0CA678", "#F59F00", "#7048E8", "#D6336C", "#1098AD", "#5C940D" };

    public CompareForm(IStockDataSource src, SinaStockDataSource sina, Themes.ThemeColors theme, string? defaultCodes = null)
    {
        _src = src; _sina = sina; _theme = theme;
        Text = "个股对比 / 大盘联动";
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(1100, 680);

        var top = new Panel { Dock = DockStyle.Top, Height = 50, BackColor = theme.PanelBack };
        var lbl = new Label { Text = "代码（逗号分隔，含指数 sh000001/sz399001）：", Left = 10, Top = 16, Width = 320, ForeColor = theme.WinFore };
        _txtCodes = new TextBox { Left = 330, Top = 12, Width = 400, Text = defaultCodes ?? "sh000001,sz399001,sh600519" };
        var lbl2 = new Label { Text = "天数：", Left = 740, Top = 16, Width = 50, ForeColor = theme.WinFore };
        _numDays = new NumericUpDown { Left = 790, Top = 12, Width = 80, Minimum = 30, Maximum = 1000, Value = 250 };
        var btn = new Button { Text = "对比", Left = 880, Top = 11, Width = 80 };
        btn.Click += async (_, _) => await DoCompareAsync();
        top.Controls.AddRange(new Control[] { lbl, _txtCodes, lbl2, _numDays, btn });
        Controls.Add(_plot);
        Controls.Add(top);
        Themes.ApplyToControls(this, theme);
        Themes.ApplyToPlot(_plot.Plot, theme);
        Load += async (_, _) => await DoCompareAsync();
    }

    private async Task DoCompareAsync()
    {
        var codes = _txtCodes.Text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (codes.Length == 0) return;
        int days = (int)_numDays.Value;

        var p = _plot.Plot; p.Clear();
        UseWaitCursor = true;
        try
        {
            for (int idx = 0; idx < codes.Length; idx++)
            {
                var code = codes[idx];
                var info = code.Length >= 2 && (code.StartsWith("sh") || code.StartsWith("sz") || code.StartsWith("bj"))
                    ? new StockInfo { FullCode = code, Code = code.Substring(2), Market = code.Substring(0, 2), Name = code }
                    : SinaStockDataSource.BuildFromCode(code);

                IReadOnlyList<Kline> bars;
                try { bars = await _src.GetKlineAsync(info, KlinePeriod.Daily, days); }
                catch { bars = await _sina.GetKlineAsync(info, KlinePeriod.Daily, days); }

                if (bars.Count == 0) continue;

                var quote = await _sina.GetQuoteAsync(info);
                if (quote != null && !string.IsNullOrEmpty(quote.Name)) info.Name = quote.Name;

                double baseClose = bars[0].Close;
                if (baseClose <= 0) continue;
                var xs = bars.Select(b => b.Date.ToOADate()).ToArray();
                var ys = bars.Select(b => (b.Close / baseClose - 1.0) * 100.0).ToArray();
                var sc = p.Add.Scatter(xs, ys);
                sc.Color = ScottPlot.Color.FromHex(Palette[idx % Palette.Length]);
                sc.LineWidth = 1.6f; sc.MarkerSize = 0;
                sc.LegendText = $"{info.Code} {info.Name}  {(ys[^1] >= 0 ? "+" : "")}{ys[^1]:F2}%";
            }
            var hl = p.Add.HorizontalLine(0); hl.Color = _theme.Axis.WithAlpha(0.4); hl.LinePattern = LinePattern.Dotted;
            p.Title("起始日归一化对比 (单位 %)");
            p.YLabel("累计涨跌幅 (%)");
            p.Axes.DateTimeTicksBottom();
            p.ShowLegend(Alignment.UpperLeft);
            Themes.ApplyToPlot(p, _theme);
            _plot.Refresh();
        }
        finally { UseWaitCursor = false; }
    }
}
