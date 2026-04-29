using ScottPlot;
using ScottPlot.WinForms;
using StockTechAnalyzer.Models;
using StockTechAnalyzer.Services;
using Color = System.Drawing.Color;
using Label = System.Windows.Forms.Label;

namespace StockTechAnalyzer.UI;

internal sealed class MinuteForm : Form
{
    private readonly StockInfo _stock;
    private readonly MinuteDataSource _src = new();
    private readonly FormsPlot _plotPrice = new() { Dock = DockStyle.Fill };
    private readonly FormsPlot _plotVol = new() { Dock = DockStyle.Fill };
    private readonly Themes.ThemeColors _theme;

    public MinuteForm(StockInfo stock, Themes.ThemeColors theme)
    {
        _stock = stock; _theme = theme;
        Text = $"分时图 — {stock.Code} {stock.Name}";
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(900, 600);
        var grid = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2 };
        grid.RowStyles.Add(new RowStyle(SizeType.Percent, 70));
        grid.RowStyles.Add(new RowStyle(SizeType.Percent, 30));
        grid.Controls.Add(_plotPrice, 0, 0);
        grid.Controls.Add(_plotVol, 0, 1);
        Controls.Add(grid);
        Themes.ApplyToControls(this, theme);
        Themes.ApplyToPlot(_plotPrice.Plot, theme);
        Themes.ApplyToPlot(_plotVol.Plot, theme);
        Load += async (_, _) => await LoadAsync();
    }

    private async Task LoadAsync()
    {
        try
        {
            var ticks = await _src.GetTodayAsync(_stock);
            if (ticks.Count == 0)
            {
                MessageBox.Show(this, "未取到分时数据（非交易时段或接口异常）。", "提示");
                return;
            }
            var xs = ticks.Select(t => t.Time.ToOADate()).ToArray();
            var ys = ticks.Select(t => t.Price).ToArray();
            var avg = ticks.Select(t => t.AvgPrice).ToArray();
            var vol = ticks.Select(t => t.Volume).ToArray();

            var p = _plotPrice.Plot;
            p.Clear();
            var price = p.Add.Scatter(xs, ys);
            price.Color = _theme.RedUp; price.LineWidth = 1.5f; price.MarkerSize = 0;
            price.LegendText = "价格";
            var avgLine = p.Add.Scatter(xs, avg);
            avgLine.Color = ScottPlot.Color.FromHex("#FFB300"); avgLine.LineWidth = 1.2f; avgLine.MarkerSize = 0;
            avgLine.LegendText = "均价";
            // 昨收参考
            double pre = ticks[0].AvgPrice;
            var hl = p.Add.HorizontalLine(pre);
            hl.Color = _theme.Axis.WithAlpha(0.4); hl.LinePattern = LinePattern.Dotted;
            p.Title($"{_stock.Code} {_stock.Name}  当日分时");
            p.Axes.DateTimeTicksBottom();
            p.ShowLegend(Alignment.UpperLeft);
            Themes.ApplyToPlot(p, _theme);
            _plotPrice.Refresh();

            var pv = _plotVol.Plot;
            pv.Clear();
            var bars = new List<Bar>();
            for (int i = 0; i < ticks.Count; i++)
            {
                bool up = i == 0 || ticks[i].Price >= ticks[i - 1].Price;
                bars.Add(new Bar
                {
                    Position = xs[i],
                    Value = vol[i],
                    FillColor = up ? _theme.RedUp : _theme.GreenDown,
                    LineColor = up ? _theme.RedUp : _theme.GreenDown,
                    Size = 1.0 / 1440.0 * 0.8,
                });
            }
            pv.Add.Bars(bars);
            pv.Title("成交量");
            pv.Axes.DateTimeTicksBottom();
            Themes.ApplyToPlot(pv, _theme);
            _plotVol.Refresh();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "加载失败：" + ex.Message, "错误");
        }
    }
}
