using System.ComponentModel;
using ScottPlot;
using ScottPlot.WinForms;
using StockTechAnalyzer.Analysis;
using StockTechAnalyzer.Indicators;
using StockTechAnalyzer.Models;
using StockTechAnalyzer.Services;
using StockTechAnalyzer.Storage;
using Color = System.Drawing.Color;
using FontStyle = System.Drawing.FontStyle;
using Label = System.Windows.Forms.Label;
using Orientation = System.Windows.Forms.Orientation;

namespace StockTechAnalyzer.UI;

internal sealed class MainForm : Form
{
    // ---------------- 状态 ----------------
    private AppSettings _settings;
    private IStockDataSource _dataSource = null!;
    private readonly SinaStockDataSource _sina = new();

    private StockInfo? _currentStock;
    private IReadOnlyList<Kline> _currentBars = Array.Empty<Kline>();
    private CancellationTokenSource? _loadCts;

    // ---------------- 控件 ----------------
    private readonly TextBox _txtSearch;
    private readonly ListBox _lstSearch;
    private readonly ListBox _lstWatch;
    private readonly Button _btnAddWatch, _btnRemoveWatch, _btnRefresh, _btnSettings;
    private readonly ComboBox _cboPeriod;
    private readonly NumericUpDown _numCount;
    private readonly Label _lblHeader;
    private readonly TextBox _txtReport;

    private readonly FormsPlot _plotPrice = new() { Dock = DockStyle.Fill };
    private readonly FormsPlot _plotVolume = new() { Dock = DockStyle.Fill };
    private readonly FormsPlot _plotMacd = new() { Dock = DockStyle.Fill };
    private readonly FormsPlot _plotKdj = new() { Dock = DockStyle.Fill };
    private readonly FormsPlot _plotChip = new() { Dock = DockStyle.Fill };

    public MainForm()
    {
        Text = "股票技术分析工具 — StockTechAnalyzer";
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(1480, 880);
        MinimumSize = new Size(1100, 700);
        Font = new Font("Microsoft YaHei UI", 9f);

        _settings = AppSettings.Load();
        ApplyDataSource();

        // ---- 顶部工具栏 ----
        var tool = new Panel { Dock = DockStyle.Top, Height = 50, BackColor = Color.FromArgb(245, 245, 248) };
        _txtSearch = new TextBox { Left = 12, Top = 12, Width = 200, PlaceholderText = "代码或名称，回车搜索" };
        _txtSearch.KeyDown += async (_, e) =>
        {
            if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; await DoSearchAsync(); }
        };
        var btnSearch = new Button { Text = "搜索", Left = 218, Top = 11, Width = 60 };
        btnSearch.Click += async (_, _) => await DoSearchAsync();

        var lblP = new Label { Text = "周期：", Left = 300, Top = 16, Width = 50 };
        _cboPeriod = new ComboBox
        {
            Left = 350, Top = 12, Width = 80,
            DropDownStyle = ComboBoxStyle.DropDownList,
        };
        _cboPeriod.Items.AddRange(new object[] { "日线", "周线", "月线" });
        _cboPeriod.SelectedIndex = 0;
        _cboPeriod.SelectedIndexChanged += async (_, _) => await ReloadAsync();

        var lblN = new Label { Text = "数量：", Left = 442, Top = 16, Width = 50 };
        _numCount = new NumericUpDown
        {
            Left = 492, Top = 12, Width = 80,
            Minimum = 30, Maximum = 1000, Value = 250, Increment = 10,
        };

        _btnRefresh = new Button { Text = "刷新", Left = 580, Top = 11, Width = 70 };
        _btnRefresh.Click += async (_, _) => await ReloadAsync();

        _btnSettings = new Button { Text = "设置", Left = 656, Top = 11, Width = 70 };
        _btnSettings.Click += (_, _) => OpenSettings();

        _lblHeader = new Label
        {
            Left = 740, Top = 8, Width = 700, Height = 36,
            Font = new Font("Microsoft YaHei UI", 11f, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleLeft,
            Text = "请选择股票",
        };

        tool.Controls.AddRange(new Control[]
        {
            _txtSearch, btnSearch, lblP, _cboPeriod, lblN, _numCount,
            _btnRefresh, _btnSettings, _lblHeader,
        });

        // ---- 左侧：搜索结果 + 自选 ----
        var leftPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4,
        };
        leftPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));
        leftPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 45));
        leftPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));
        leftPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 55));

        leftPanel.Controls.Add(new Label { Text = " 搜索结果", Dock = DockStyle.Fill, BackColor = Color.Gainsboro }, 0, 0);
        _lstSearch = new ListBox { Dock = DockStyle.Fill, IntegralHeight = false };
        _lstSearch.DoubleClick += async (_, _) => await SelectStockAsync(_lstSearch.SelectedItem as StockInfo);
        leftPanel.Controls.Add(_lstSearch, 0, 1);

        var headerWatch = new Panel { Dock = DockStyle.Fill, BackColor = Color.Gainsboro };
        var lblWatch = new Label { Text = " 自选股", Dock = DockStyle.Left, AutoSize = false, Width = 100, TextAlign = ContentAlignment.MiddleLeft };
        _btnAddWatch = new Button { Text = "+", Dock = DockStyle.Right, Width = 28 };
        _btnRemoveWatch = new Button { Text = "−", Dock = DockStyle.Right, Width = 28 };
        _btnAddWatch.Click += (_, _) => AddCurrentToWatchlist();
        _btnRemoveWatch.Click += (_, _) => RemoveSelectedFromWatchlist();
        headerWatch.Controls.AddRange(new Control[] { lblWatch, _btnAddWatch, _btnRemoveWatch });
        leftPanel.Controls.Add(headerWatch, 0, 2);

        _lstWatch = new ListBox { Dock = DockStyle.Fill, IntegralHeight = false };
        _lstWatch.DoubleClick += async (_, _) => await SelectStockAsync(_lstWatch.SelectedItem as StockInfo);
        leftPanel.Controls.Add(_lstWatch, 0, 3);
        RefreshWatchlistUi();

        // ---- 中间：图表 Tabs ----
        var tabs = new TabControl { Dock = DockStyle.Fill };

        var pgKline = new TabPage("K线 + 指标");
        var chartGrid = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4,
            BackColor = Color.White,
        };
        chartGrid.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
        chartGrid.RowStyles.Add(new RowStyle(SizeType.Percent, 16));
        chartGrid.RowStyles.Add(new RowStyle(SizeType.Percent, 17));
        chartGrid.RowStyles.Add(new RowStyle(SizeType.Percent, 17));
        chartGrid.Controls.Add(_plotPrice, 0, 0);
        chartGrid.Controls.Add(_plotVolume, 0, 1);
        chartGrid.Controls.Add(_plotMacd, 0, 2);
        chartGrid.Controls.Add(_plotKdj, 0, 3);
        pgKline.Controls.Add(chartGrid);

        var pgChip = new TabPage("筹码分布");
        pgChip.Controls.Add(_plotChip);

        tabs.TabPages.AddRange(new[] { pgKline, pgChip });

        // ---- 右侧：分析报告 ----
        _txtReport = new TextBox
        {
            Dock = DockStyle.Fill, Multiline = true, ReadOnly = true,
            ScrollBars = ScrollBars.Vertical, WordWrap = true,
            Font = new Font("Consolas", 10f),
            BackColor = Color.FromArgb(252, 252, 250),
            Text = "选择股票后此处显示综合分析报告。",
        };
        var rightPanel = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2 };
        rightPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));
        rightPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        rightPanel.Controls.Add(new Label { Text = " 综合分析", Dock = DockStyle.Fill, BackColor = Color.Gainsboro }, 0, 0);
        rightPanel.Controls.Add(_txtReport, 0, 1);

        // ---- 主分隔 ----
        var splitMain = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Vertical, SplitterDistance = 200 };
        splitMain.Panel1.Controls.Add(leftPanel);

        var splitRight = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Vertical };
        splitRight.Panel1.Controls.Add(tabs);
        splitRight.Panel2.Controls.Add(rightPanel);
        splitMain.Panel2.Controls.Add(splitRight);

        Controls.Add(splitMain);
        Controls.Add(tool);

        Load += (_, _) =>
        {
            splitMain.SplitterDistance = 200;
            splitRight.SplitterDistance = (int)(splitRight.Width * 0.72);
            ConfigureEmptyPlots();
        };

        FormClosing += (_, _) => _settings.Save();
    }

    // ============================================================
    // 数据源
    // ============================================================
    private void ApplyDataSource()
    {
        _dataSource = _settings.DataSource == "Tushare"
            ? new TushareStockDataSource(_settings.TushareToken, _sina)
            : _sina;
    }

    private void OpenSettings()
    {
        using var dlg = new SettingsForm(_settings);
        if (dlg.ShowDialog(this) == DialogResult.OK)
        {
            _settings.DataSource = dlg.DataSource;
            _settings.TushareToken = dlg.TushareToken;
            _settings.Save();
            ApplyDataSource();
            MessageBox.Show(this, $"已切换至：{_dataSource.Name}", "设置", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }

    // ============================================================
    // 搜索 / 选股
    // ============================================================
    private async Task DoSearchAsync()
    {
        var kw = _txtSearch.Text.Trim();
        if (string.IsNullOrEmpty(kw)) return;
        try
        {
            _lstSearch.Items.Clear();
            var results = await _sina.SearchAsync(kw); // 搜索固定走新浪
            foreach (var r in results) _lstSearch.Items.Add(r);
            if (results.Count == 0) _lstSearch.Items.Add("(无匹配结果)");
            else if (results.Count == 1) await SelectStockAsync(results[0]);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "搜索失败：" + ex.Message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private async Task SelectStockAsync(StockInfo? stock)
    {
        if (stock == null) return;
        _currentStock = stock;
        await ReloadAsync();
    }

    private async Task ReloadAsync()
    {
        if (_currentStock == null) return;

        _loadCts?.Cancel();
        _loadCts = new CancellationTokenSource();
        var ct = _loadCts.Token;

        var period = _cboPeriod.SelectedIndex switch
        {
            1 => KlinePeriod.Weekly,
            2 => KlinePeriod.Monthly,
            _ => KlinePeriod.Daily,
        };
        int count = (int)_numCount.Value;

        _lblHeader.Text = $"加载中：{_currentStock.Code} {_currentStock.Name} ...";
        UseWaitCursor = true;
        try
        {
            var bars = await _dataSource.GetKlineAsync(_currentStock, period, count, ct);
            var quote = await _sina.GetQuoteAsync(_currentStock, ct);
            if (ct.IsCancellationRequested) return;

            _currentBars = bars;
            if (quote != null && !string.IsNullOrEmpty(quote.Name))
                _currentStock.Name = quote.Name;

            UpdateHeader(quote);
            RenderAll();
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            MessageBox.Show(this, "加载失败：" + ex.Message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            _lblHeader.Text = "加载失败";
        }
        finally { UseWaitCursor = false; }
    }

    private void UpdateHeader(RealtimeQuote? quote)
    {
        if (_currentStock == null) return;
        var src = _dataSource.Name;
        if (quote != null && quote.Last > 0)
        {
            var sign = quote.ChangePct >= 0 ? "+" : "";
            _lblHeader.Text = $"{_currentStock.Code} {_currentStock.Name}   现价 {quote.Last:F2}   {sign}{quote.ChangePct:F2}%   [{src}]";
            _lblHeader.ForeColor = quote.ChangePct >= 0 ? Color.Crimson : Color.ForestGreen;
        }
        else
        {
            _lblHeader.Text = $"{_currentStock.Code} {_currentStock.Name}   [{src}]";
            _lblHeader.ForeColor = Color.Black;
        }
    }

    // ============================================================
    // 自选股
    // ============================================================
    private void RefreshWatchlistUi()
    {
        _lstWatch.Items.Clear();
        foreach (var s in _settings.Watchlist) _lstWatch.Items.Add(s);
    }

    private void AddCurrentToWatchlist()
    {
        if (_currentStock == null) return;
        if (_settings.Watchlist.Any(s => s.FullCode == _currentStock.FullCode)) return;
        _settings.Watchlist.Add(new StockInfo
        {
            Code = _currentStock.Code,
            Name = _currentStock.Name,
            FullCode = _currentStock.FullCode,
            Market = _currentStock.Market,
        });
        _settings.Save();
        RefreshWatchlistUi();
    }

    private void RemoveSelectedFromWatchlist()
    {
        if (_lstWatch.SelectedItem is not StockInfo s) return;
        _settings.Watchlist.RemoveAll(x => x.FullCode == s.FullCode);
        _settings.Save();
        RefreshWatchlistUi();
    }

    // ============================================================
    // 渲染
    // ============================================================
    private void ConfigureEmptyPlots()
    {
        foreach (var p in new[] { _plotPrice, _plotVolume, _plotMacd, _plotKdj, _plotChip })
        {
            p.Plot.Axes.DateTimeTicksBottom();
            p.Refresh();
        }
    }

    private void RenderAll()
    {
        if (_currentBars.Count == 0) return;
        RenderPriceChart();
        RenderVolumeChart();
        RenderMacdChart();
        RenderKdjChart();
        var chip = ChipDistribution.Calculate(_currentBars);
        RenderChipChart(chip);
        var report = SentimentAnalyzer.Analyze(_currentBars, chip);
        _txtReport.Text = SentimentAnalyzer.Format(report);
    }

    private static readonly ScottPlot.Color RedUp = ScottPlot.Color.FromHex("#E03131");
    private static readonly ScottPlot.Color GreenDown = ScottPlot.Color.FromHex("#2F9E44");

    private void RenderPriceChart()
    {
        var p = _plotPrice.Plot;
        p.Clear();

        var bars = _currentBars;
        var ohlcs = new List<OHLC>(bars.Count);
        TimeSpan span = _cboPeriod.SelectedIndex switch
        {
            1 => TimeSpan.FromDays(5),
            2 => TimeSpan.FromDays(20),
            _ => TimeSpan.FromHours(20),
        };
        foreach (var b in bars)
            ohlcs.Add(new OHLC(b.Open, b.High, b.Low, b.Close, b.Date, span));

        var cs = p.Add.Candlestick(ohlcs);
        cs.RisingFillStyle.Color = RedUp;
        cs.FallingFillStyle.Color = GreenDown;
        cs.RisingLineStyle.Color = RedUp;
        cs.FallingLineStyle.Color = GreenDown;

        var closes = bars.Select(b => b.Close).ToArray();
        var dates = bars.Select(b => b.Date.ToOADate()).ToArray();

        AddMa(p, dates, closes, 5, ScottPlot.Color.FromHex("#F59F00"));
        AddMa(p, dates, closes, 10, ScottPlot.Color.FromHex("#1971C2"));
        AddMa(p, dates, closes, 20, ScottPlot.Color.FromHex("#7048E8"));
        AddMa(p, dates, closes, 60, ScottPlot.Color.FromHex("#0CA678"));

        // BOLL
        var boll = Indicators.Indicators.BOLL(closes);
        AddLine(p, dates, boll.Upper, ScottPlot.Color.FromHex("#868E96"), "BOLL上");
        AddLine(p, dates, boll.Mid, ScottPlot.Color.FromHex("#495057"), "BOLL中");
        AddLine(p, dates, boll.Lower, ScottPlot.Color.FromHex("#868E96"), "BOLL下");

        p.Title($"{_currentStock!.Code} {_currentStock.Name}  K线 / 均线 / BOLL");
        p.Axes.DateTimeTicksBottom();
        p.ShowLegend(Alignment.UpperLeft);
        _plotPrice.Refresh();
    }

    private static void AddMa(Plot p, double[] dates, double[] closes, int period, ScottPlot.Color color)
    {
        var ma = Indicators.Indicators.SMA(closes, period);
        var xs = new List<double>();
        var ys = new List<double>();
        for (int i = 0; i < ma.Length; i++)
        {
            if (double.IsNaN(ma[i])) continue;
            xs.Add(dates[i]); ys.Add(ma[i]);
        }
        if (xs.Count == 0) return;
        var sc = p.Add.Scatter(xs.ToArray(), ys.ToArray());
        sc.LineWidth = 1.4f;
        sc.MarkerSize = 0;
        sc.Color = color;
        sc.LegendText = $"MA{period}";
    }

    private static void AddLine(Plot p, double[] dates, double[] vals, ScottPlot.Color color, string label)
    {
        var xs = new List<double>();
        var ys = new List<double>();
        for (int i = 0; i < vals.Length; i++)
        {
            if (double.IsNaN(vals[i])) continue;
            xs.Add(dates[i]); ys.Add(vals[i]);
        }
        if (xs.Count == 0) return;
        var sc = p.Add.Scatter(xs.ToArray(), ys.ToArray());
        sc.LineWidth = 1f;
        sc.MarkerSize = 0;
        sc.Color = color;
        sc.LineStyle.Pattern = LinePattern.Dashed;
        sc.LegendText = label;
    }

    private void RenderVolumeChart()
    {
        var p = _plotVolume.Plot;
        p.Clear();

        var bars = _currentBars;
        var blist = new List<Bar>(bars.Count);
        for (int i = 0; i < bars.Count; i++)
        {
            var b = bars[i];
            bool up = b.Close >= b.Open;
            blist.Add(new Bar
            {
                Position = b.Date.ToOADate(),
                Value = b.Volume,
                FillColor = up ? RedUp : GreenDown,
                LineColor = up ? RedUp : GreenDown,
                Size = 0.7,
            });
        }
        p.Add.Bars(blist);
        p.Title("成交量");
        p.Axes.DateTimeTicksBottom();
        _plotVolume.Refresh();
    }

    private void RenderMacdChart()
    {
        var p = _plotMacd.Plot;
        p.Clear();

        var closes = _currentBars.Select(b => b.Close).ToArray();
        var dates = _currentBars.Select(b => b.Date.ToOADate()).ToArray();
        var macd = Indicators.Indicators.MACD(closes);

        // 柱
        var blist = new List<Bar>();
        for (int i = 0; i < macd.Hist.Length; i++)
        {
            blist.Add(new Bar
            {
                Position = dates[i],
                Value = macd.Hist[i],
                FillColor = macd.Hist[i] >= 0 ? RedUp : GreenDown,
                LineColor = macd.Hist[i] >= 0 ? RedUp : GreenDown,
                Size = 0.6,
            });
        }
        p.Add.Bars(blist);

        var difLine = p.Add.Scatter(dates, macd.Dif);
        difLine.MarkerSize = 0; difLine.LineWidth = 1.3f;
        difLine.Color = ScottPlot.Color.FromHex("#1971C2");
        difLine.LegendText = "DIF";

        var deaLine = p.Add.Scatter(dates, macd.Dea);
        deaLine.MarkerSize = 0; deaLine.LineWidth = 1.3f;
        deaLine.Color = ScottPlot.Color.FromHex("#F59F00");
        deaLine.LegendText = "DEA";

        p.Title("MACD (12,26,9)");
        p.Axes.DateTimeTicksBottom();
        p.ShowLegend(Alignment.UpperLeft);
        _plotMacd.Refresh();
    }

    private void RenderKdjChart()
    {
        var p = _plotKdj.Plot;
        p.Clear();

        var dates = _currentBars.Select(b => b.Date.ToOADate()).ToArray();
        var kdj = Indicators.Indicators.KDJ(_currentBars);
        var rsi = Indicators.Indicators.RSI(_currentBars.Select(b => b.Close).ToArray());

        var lk = p.Add.Scatter(dates, kdj.K); lk.MarkerSize = 0; lk.LineWidth = 1.2f;
        lk.Color = ScottPlot.Color.FromHex("#1971C2"); lk.LegendText = "K";
        var ld = p.Add.Scatter(dates, kdj.D); ld.MarkerSize = 0; ld.LineWidth = 1.2f;
        ld.Color = ScottPlot.Color.FromHex("#F59F00"); ld.LegendText = "D";
        var lj = p.Add.Scatter(dates, kdj.J); lj.MarkerSize = 0; lj.LineWidth = 1.2f;
        lj.Color = ScottPlot.Color.FromHex("#E03131"); lj.LegendText = "J";

        // RSI 用浅灰副线（共享同一坐标，0-100 同尺度）
        var lr = p.Add.Scatter(dates, rsi); lr.MarkerSize = 0; lr.LineWidth = 1.0f;
        lr.Color = ScottPlot.Color.FromHex("#7048E8"); lr.LegendText = "RSI14";
        lr.LineStyle.Pattern = LinePattern.Dashed;

        // 参考线 20/80
        var hl1 = p.Add.HorizontalLine(80); hl1.Color = ScottPlot.Color.FromHex("#ADB5BD"); hl1.LinePattern = LinePattern.Dotted;
        var hl2 = p.Add.HorizontalLine(20); hl2.Color = ScottPlot.Color.FromHex("#ADB5BD"); hl2.LinePattern = LinePattern.Dotted;

        p.Title("KDJ (9,3,3) / RSI(14)");
        p.Axes.DateTimeTicksBottom();
        p.ShowLegend(Alignment.UpperLeft);
        _plotKdj.Refresh();
    }

    private void RenderChipChart(ChipDistribution.Result chip)
    {
        var p = _plotChip.Plot;
        p.Clear();

        if (chip.Prices.Length == 0) { _plotChip.Refresh(); return; }

        // 横向柱：x = 筹码, y = 价格
        double maxChip = chip.Chips.Max();
        if (maxChip <= 0) { _plotChip.Refresh(); return; }

        double step = chip.Prices.Length > 1 ? chip.Prices[1] - chip.Prices[0] : 0.01;
        var blist = new List<Bar>();
        for (int i = 0; i < chip.Prices.Length; i++)
        {
            blist.Add(new Bar
            {
                Position = chip.Prices[i],
                Value = chip.Chips[i] / maxChip,  // 归一化
                FillColor = ScottPlot.Color.FromHex("#4DABF7"),
                LineColor = ScottPlot.Color.FromHex("#4DABF7"),
                Size = step * 0.9,
                Orientation = ScottPlot.Orientation.Horizontal,
            });
        }
        p.Add.Bars(blist);

        // 当前价 + 平均成本
        double cur = _currentBars[^1].Close;
        var hlCur = p.Add.HorizontalLine(cur);
        hlCur.Color = RedUp; hlCur.LineWidth = 2;
        hlCur.Text = $"现价 {cur:F2}";
        hlCur.LabelOppositeAxis = true;

        var hlAvg = p.Add.HorizontalLine(chip.AvgCost);
        hlAvg.Color = ScottPlot.Color.FromHex("#7048E8"); hlAvg.LineWidth = 2;
        hlAvg.LinePattern = LinePattern.Dashed;
        hlAvg.Text = $"均成本 {chip.AvgCost:F2}";
        hlAvg.LabelOppositeAxis = true;

        p.Title($"筹码分布   获利盘 {chip.ProfitRatio:P1}   70%集中度 {chip.Concentration70:P1}");
        p.XLabel("筹码相对密度");
        p.YLabel("价格");
        _plotChip.Refresh();
    }
}
