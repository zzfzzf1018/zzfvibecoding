using System.Text;
using ScottPlot;
using ScottPlot.WinForms;
using StockTechAnalyzer.Backtest;
using StockTechAnalyzer.Models;
using Color = System.Drawing.Color;
using Label = System.Windows.Forms.Label;

namespace StockTechAnalyzer.UI;

internal sealed class BacktestForm : Form
{
    private readonly StockInfo _stock;
    private readonly IReadOnlyList<Kline> _bars;
    private readonly Themes.ThemeColors _theme;
    private readonly ComboBox _cboStrategy;
    private readonly NumericUpDown _numCash, _numFee;
    private readonly TextBox _txtSummary;
    private readonly DataGridView _grid;
    private readonly FormsPlot _plot = new() { Dock = DockStyle.Fill };

    public BacktestForm(StockInfo stock, IReadOnlyList<Kline> bars, Themes.ThemeColors theme)
    {
        _stock = stock; _bars = bars; _theme = theme;
        Text = $"策略回测 — {stock.Code} {stock.Name}";
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(1200, 720);

        var top = new Panel { Dock = DockStyle.Top, Height = 50, BackColor = theme.PanelBack };
        new Label { Text = "策略：", Left = 10, Top = 16, Width = 50, ForeColor = theme.WinFore, Parent = top };
        _cboStrategy = new ComboBox { Left = 60, Top = 12, Width = 220, DropDownStyle = ComboBoxStyle.DropDownList, Parent = top };
        _cboStrategy.Items.AddRange(new object[] { "MACD 金叉/死叉", "KDJ 金叉/死叉", "MA5 上穿/下穿 MA20", "BOLL 突破上轨/跌破中轨", "买入持有 (基准)" });
        _cboStrategy.SelectedIndex = 0;

        new Label { Text = "初始资金：", Left = 290, Top = 16, Width = 70, ForeColor = theme.WinFore, Parent = top };
        _numCash = new NumericUpDown { Left = 360, Top = 12, Width = 110, Minimum = 10000, Maximum = 100000000, Value = 100000, Increment = 10000, Parent = top };
        new Label { Text = "费率(‰)：", Left = 480, Top = 16, Width = 70, ForeColor = theme.WinFore, Parent = top };
        _numFee = new NumericUpDown { Left = 550, Top = 12, Width = 60, DecimalPlaces = 2, Minimum = 0, Maximum = 50, Value = 0.30M, Increment = 0.05M, Parent = top };

        var btn = new Button { Text = "运行回测", Left = 620, Top = 11, Width = 100, Parent = top };
        btn.Click += (_, _) => RunOnce();
        var btnAll = new Button { Text = "全策略对比", Left = 728, Top = 11, Width = 100, Parent = top };
        btnAll.Click += (_, _) => RunAll();

        _txtSummary = new TextBox
        {
            Dock = DockStyle.Top, Height = 130, Multiline = true, ReadOnly = true,
            Font = new Font("Consolas", 10f), ScrollBars = ScrollBars.Vertical,
            Text = "选择策略后点击运行回测。",
        };

        _grid = new DataGridView
        {
            Dock = DockStyle.Right, Width = 380,
            ReadOnly = true, AllowUserToAddRows = false,
            RowHeadersVisible = false, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
        };
        _grid.Columns.Add("buy", "买入");
        _grid.Columns.Add("sell", "卖出");
        _grid.Columns.Add("ret", "收益%");

        Controls.Add(_plot);
        Controls.Add(_grid);
        Controls.Add(_txtSummary);
        Controls.Add(top);

        Themes.ApplyToControls(this, theme);
        Themes.ApplyToPlot(_plot.Plot, theme);
    }

    private void RunOnce()
    {
        var s = (BacktestEngine.Strategy)_cboStrategy.SelectedIndex;
        var r = BacktestEngine.Run(_bars, s, (double)_numCash.Value, (double)_numFee.Value / 1000.0);
        ShowResult(new[] { r });
    }

    private void RunAll()
    {
        var list = new List<BacktestEngine.Result>();
        foreach (BacktestEngine.Strategy s in Enum.GetValues(typeof(BacktestEngine.Strategy)))
            list.Add(BacktestEngine.Run(_bars, s, (double)_numCash.Value, (double)_numFee.Value / 1000.0));
        ShowResult(list);
    }

    private void ShowResult(IList<BacktestEngine.Result> results)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"区间：{_bars[0].Date:yyyy-MM-dd} ~ {_bars[^1].Date:yyyy-MM-dd}    样本：{_bars.Count} 根");
        sb.AppendLine();
        sb.AppendLine($"{"策略",-22}{"总收益",10}{"年化",10}{"最大回撤",12}{"胜率",10}{"交易次数",10}");
        foreach (var r in results)
            sb.AppendLine($"{NameOf(r.Strategy),-22}{r.TotalReturn,10:P2}{r.AnnualReturn,10:P2}{r.MaxDrawdown,12:P2}{r.WinRate,10:P0}{r.TradeCount,10}");
        _txtSummary.Text = sb.ToString();

        // 画净值曲线
        var p = _plot.Plot;
        p.Clear();
        string[] colors = { "#E03131", "#1971C2", "#0CA678", "#F59F00", "#7048E8" };
        for (int i = 0; i < results.Count; i++)
        {
            var r = results[i];
            if (r.Equity.Count == 0) continue;
            var xs = r.Equity.Select(e => e.Date.ToOADate()).ToArray();
            var ys = r.Equity.Select(e => e.Equity / r.InitCapital).ToArray();
            var sc = p.Add.Scatter(xs, ys);
            sc.Color = ScottPlot.Color.FromHex(colors[i % colors.Length]);
            sc.LineWidth = 1.6f; sc.MarkerSize = 0;
            sc.LegendText = $"{NameOf(r.Strategy)}  {r.TotalReturn:+0.00%;-0.00%;0%}";
        }
        var hl = p.Add.HorizontalLine(1.0); hl.Color = _theme.Axis.WithAlpha(0.4); hl.LinePattern = LinePattern.Dotted;
        p.Title("策略净值曲线 (起始 = 1)");
        p.Axes.DateTimeTicksBottom();
        p.ShowLegend(Alignment.UpperLeft);
        Themes.ApplyToPlot(p, _theme);
        _plot.Refresh();

        // 表格只显示第一个策略的交易明细
        _grid.Rows.Clear();
        foreach (var t in results[0].Trades)
        {
            int row = _grid.Rows.Add(
                $"{t.BuyDate:yy-MM-dd} {t.BuyPrice:F2}",
                $"{t.SellDate:yy-MM-dd} {t.SellPrice:F2}",
                $"{t.Return:P2}");
            _grid.Rows[row].DefaultCellStyle.ForeColor = t.Return >= 0 ? Color.Crimson : Color.LimeGreen;
        }
    }

    private static string NameOf(BacktestEngine.Strategy s) => s switch
    {
        BacktestEngine.Strategy.MacdCross => "MACD 金叉/死叉",
        BacktestEngine.Strategy.KdjCross => "KDJ 金叉/死叉",
        BacktestEngine.Strategy.Ma5Cross20 => "MA5/MA20 金叉",
        BacktestEngine.Strategy.BollBreak => "BOLL 突破",
        BacktestEngine.Strategy.BuyAndHold => "买入持有",
        _ => s.ToString(),
    };
}
