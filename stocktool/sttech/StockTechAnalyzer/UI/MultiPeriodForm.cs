using ScottPlot;
using ScottPlot.WinForms;
using StockTechAnalyzer.Models;
using StockTechAnalyzer.Services;
using Color = System.Drawing.Color;

namespace StockTechAnalyzer.UI;

internal sealed class MultiPeriodForm : Form
{
    private readonly StockInfo _stock;
    private readonly IStockDataSource _src;
    private readonly Themes.ThemeColors _theme;
    private readonly FormsPlot _pDay = new() { Dock = DockStyle.Fill };
    private readonly FormsPlot _pWeek = new() { Dock = DockStyle.Fill };
    private readonly FormsPlot _pMonth = new() { Dock = DockStyle.Fill };

    public MultiPeriodForm(StockInfo stock, IStockDataSource src, Themes.ThemeColors theme)
    {
        _stock = stock; _src = src; _theme = theme;
        Text = $"多周期对比 — {stock.Code} {stock.Name}";
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(1400, 700);
        var grid = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 1 };
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.34f));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33f));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33f));
        grid.Controls.Add(_pDay, 0, 0);
        grid.Controls.Add(_pWeek, 1, 0);
        grid.Controls.Add(_pMonth, 2, 0);
        Controls.Add(grid);
        Themes.ApplyToControls(this, theme);
        Load += async (_, _) => await LoadAsync();
    }

    private async Task LoadAsync()
    {
        try
        {
            var t1 = _src.GetKlineAsync(_stock, KlinePeriod.Daily, 250);
            var t2 = _src.GetKlineAsync(_stock, KlinePeriod.Weekly, 200);
            var t3 = _src.GetKlineAsync(_stock, KlinePeriod.Monthly, 120);
            await Task.WhenAll(t1, t2, t3);
            DrawCandles(_pDay, "日K", t1.Result, TimeSpan.FromHours(20));
            DrawCandles(_pWeek, "周K", t2.Result, TimeSpan.FromDays(5));
            DrawCandles(_pMonth, "月K", t3.Result, TimeSpan.FromDays(20));
        }
        catch (Exception ex) { MessageBox.Show(this, ex.Message, "错误"); }
    }

    private void DrawCandles(FormsPlot fp, string title, IReadOnlyList<Kline> bars, TimeSpan span)
    {
        var p = fp.Plot;
        p.Clear();
        var ohlc = new List<OHLC>(bars.Count);
        foreach (var b in bars) ohlc.Add(new OHLC(b.Open, b.High, b.Low, b.Close, b.Date, span));
        var cs = p.Add.Candlestick(ohlc);
        cs.RisingFillStyle.Color = _theme.RedUp;
        cs.FallingFillStyle.Color = _theme.GreenDown;
        cs.RisingLineStyle.Color = _theme.RedUp;
        cs.FallingLineStyle.Color = _theme.GreenDown;

        var closes = bars.Select(b => b.Close).ToArray();
        var dates = bars.Select(b => b.Date.ToOADate()).ToArray();
        var ma20 = Indicators.Indicators.SMA(closes, 20);
        var xs = new List<double>(); var ys = new List<double>();
        for (int i = 0; i < ma20.Length; i++)
        { if (!double.IsNaN(ma20[i])) { xs.Add(dates[i]); ys.Add(ma20[i]); } }
        if (xs.Count > 0)
        {
            var ln = p.Add.Scatter(xs.ToArray(), ys.ToArray());
            ln.Color = ScottPlot.Color.FromHex("#7048E8"); ln.LineWidth = 1.4f; ln.MarkerSize = 0;
            ln.LegendText = "MA20";
        }
        p.Title($"{title}   {(bars.Count > 0 ? bars[^1].Close.ToString("F2") : "—")}");
        p.Axes.DateTimeTicksBottom();
        Themes.ApplyToPlot(p, _theme);
        fp.Refresh();
    }
}
