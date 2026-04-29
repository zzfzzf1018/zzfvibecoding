using System.Globalization;
using System.Text;
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
    private readonly EastMoneyExtras _emExtras = new();
    private readonly KlineCache _cache = new();

    private StockInfo? _currentStock;
    private IReadOnlyList<Kline> _currentBars = Array.Empty<Kline>();
    private CancellationTokenSource? _loadCts;
    private Themes.ThemeColors _theme = Themes.Light;

    // ---------------- 控件 ----------------
    private readonly TextBox _txtSearch;
    private readonly ListBox _lstSearch;
    private readonly ListBox _lstWatch;
    private readonly Button _btnAddWatch, _btnRemoveWatch, _btnRefresh, _btnSettings, _btnExport, _btnTools, _btnDraw;
    private readonly ComboBox _cboPeriod;
    private readonly NumericUpDown _numCount;
    private readonly Label _lblHeader;
    private readonly TextBox _txtReport;
    private readonly TextBox _txtExplain;
    private readonly TextBox _txtFundFlow;
    private readonly TextBox _txtRisk;

    private readonly FormsPlot _plotPrice = new() { Dock = DockStyle.Fill };
    private readonly FormsPlot _plotVolume = new() { Dock = DockStyle.Fill };
    private readonly FormsPlot _plotMacd = new() { Dock = DockStyle.Fill };
    private readonly FormsPlot _plotKdj = new() { Dock = DockStyle.Fill };
    private readonly FormsPlot _plotChip = new() { Dock = DockStyle.Fill };

    // 十字光标
    private ScottPlot.Plottables.Crosshair? _crosshair;
    private ScottPlot.Plottables.Annotation? _hoverNote;

    // 画图工具
    private DrawingTool? _drawingTool;

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

        _btnRefresh = new Button { Text = "刷新", Left = 580, Top = 11, Width = 60 };
        _btnRefresh.Click += async (_, _) => await ReloadAsync();

        _btnExport = new Button { Text = "导出 ▾", Left = 646, Top = 11, Width = 70 };
        _btnExport.Click += (_, _) => ShowExportMenu();

        _btnDraw = new Button { Text = "画图 ▾", Left = 722, Top = 11, Width = 70 };
        _btnDraw.Click += (_, _) => ShowDrawMenu();

        _btnTools = new Button { Text = "工具 ▾", Left = 798, Top = 11, Width = 70 };
        _btnTools.Click += (_, _) => ShowToolsMenu();

        _btnSettings = new Button { Text = "设置", Left = 874, Top = 11, Width = 60 };
        _btnSettings.Click += (_, _) => OpenSettings();

        _lblHeader = new Label
        {
            Left = 950, Top = 8, Width = 510, Height = 36,
            Font = new Font("Microsoft YaHei UI", 11f, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleLeft,
            Text = "请选择股票",
        };

        tool.Controls.AddRange(new Control[]
        {
            _txtSearch, btnSearch, lblP, _cboPeriod, lblN, _numCount,
            _btnRefresh, _btnExport, _btnDraw, _btnTools, _btnSettings, _lblHeader,
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

        // ---- 右侧：分析报告（4 个标签）----
        _txtReport = MakeReportBox("选择股票后此处显示综合分析报告。", monoFont: true);
        _txtExplain = MakeReportBox("选择股票后此处用大白话解读各项指标。", monoFont: false);
        _txtFundFlow = MakeReportBox("选择股票后此处显示基本面 + 资金流向。", monoFont: true);
        _txtRisk = MakeReportBox("选择股票后此处显示风险指标。", monoFont: true);

        var rightTabs = new TabControl { Dock = DockStyle.Fill };
        var pgReport = new TabPage("综合分析");      pgReport.Controls.Add(_txtReport);
        var pgExplain = new TabPage("指标解读");      pgExplain.Controls.Add(_txtExplain);
        var pgFund = new TabPage("基本面+资金");      pgFund.Controls.Add(_txtFundFlow);
        var pgRisk = new TabPage("风险");             pgRisk.Controls.Add(_txtRisk);
        rightTabs.TabPages.AddRange(new[] { pgReport, pgExplain, pgFund, pgRisk });

        var rightPanel = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2 };
        rightPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));
        rightPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        rightPanel.Controls.Add(new Label { Text = " 分析面板", Dock = DockStyle.Fill, BackColor = Color.Gainsboro }, 0, 0);
        rightPanel.Controls.Add(rightTabs, 0, 1);

        // ---- 主分隔 ----
        var splitMain = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Vertical, SplitterDistance = 200 };
        splitMain.Panel1.Controls.Add(leftPanel);

        var splitRight = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Vertical };
        splitRight.Panel1.Controls.Add(tabs);
        splitRight.Panel2.Controls.Add(rightPanel);
        splitMain.Panel2.Controls.Add(splitRight);

        Controls.Add(splitMain);
        Controls.Add(tool);

        // 鼠标悬停 -> 十字光标
        _plotPrice.MouseMove += OnPriceMouseMove;
        _plotPrice.MouseLeave += (_, _) => HideCrosshair();

        // 禁用 K 线相关 4 个图的缩放/拖拽，避免错位（画图工具自己也依赖此状态）
        DisableInteraction(_plotPrice);
        DisableInteraction(_plotVolume);
        DisableInteraction(_plotMacd);
        DisableInteraction(_plotKdj);
        DisableInteraction(_plotChip);

        Load += (_, _) =>
        {
            splitMain.SplitterDistance = 200;
            splitRight.SplitterDistance = (int)(splitRight.Width * 0.72);
            ApplyTheme();
            ConfigureEmptyPlots();
        };

        FormClosing += (_, _) => { _settings.Save(); _cache.Dispose(); };
    }

    private static TextBox MakeReportBox(string placeholder, bool monoFont) => new()
    {
        Dock = DockStyle.Fill, Multiline = true, ReadOnly = true,
        ScrollBars = ScrollBars.Vertical, WordWrap = true,
        Font = monoFont ? new Font("Consolas", 10f) : new Font("Microsoft YaHei UI", 10f),
        BackColor = Color.FromArgb(252, 252, 250),
        Text = placeholder,
    };

    // ============================================================
    // 数据源
    // ============================================================
    private void ApplyDataSource()
    {
        IStockDataSource raw = _settings.DataSource switch
        {
            "Tushare" => new TushareStockDataSource(_settings.TushareToken, _sina),
            "EastMoney" => new EastMoneyStockDataSource(_sina),
            _ => _sina,
        };
        _dataSource = _settings.EnableCache ? new CachedDataSource(raw, _cache) : raw;
    }

    private void OpenSettings()
    {
        using var dlg = new SettingsForm(_settings);
        if (dlg.ShowDialog(this) == DialogResult.OK)
        {
            bool themeChanged = _settings.DarkMode != dlg.DarkMode;
            _settings.DataSource = dlg.DataSource;
            _settings.TushareToken = dlg.TushareToken;
            _settings.DarkMode = dlg.DarkMode;
            _settings.EnableCache = dlg.EnableCache;
            _settings.Save();
            ApplyDataSource();
            if (themeChanged) ApplyTheme();
            if (_currentBars.Count > 0) RenderAll();
        }
    }

    // ============================================================
    // 主题
    // ============================================================
    private void ApplyTheme()
    {
        _theme = _settings.DarkMode ? Themes.Dark : Themes.Light;
        Themes.ApplyToControls(this, _theme);
        foreach (var fp in new[] { _plotPrice, _plotVolume, _plotMacd, _plotKdj, _plotChip })
        {
            Themes.ApplyToPlot(fp.Plot, _theme);
            fp.Refresh();
        }
    }

    // ============================================================
    // 搜索 / 选股 / 加载
    // ============================================================
    private async Task DoSearchAsync()
    {
        var kw = _txtSearch.Text.Trim();
        if (string.IsNullOrEmpty(kw)) return;
        try
        {
            _lstSearch.Items.Clear();
            var results = await _sina.SearchAsync(kw);
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

            // 异步拉取基本面 / 资金流向（不阻塞）
            _ = LoadFundFlowAsync(_currentStock, ct);
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            MessageBox.Show(this, "加载失败：" + ex.Message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            _lblHeader.Text = "加载失败";
        }
        finally { UseWaitCursor = false; }
    }

    private async Task LoadFundFlowAsync(StockInfo stock, CancellationToken ct)
    {
        try
        {
            var f = await _emExtras.GetFundamentalsAsync(stock, ct);
            var mf = await _emExtras.GetMoneyFlowAsync(stock, ct);
            if (ct.IsCancellationRequested) return;
            var sb = new StringBuilder();
            sb.Append(EastMoneyExtras.FormatFundamentals(f));
            sb.AppendLine();
            sb.Append(EastMoneyExtras.FormatMoneyFlow(mf));
            if (IsHandleCreated && !IsDisposed)
                BeginInvoke(() => _txtFundFlow.Text = sb.ToString());
        }
        catch { /* 失败静默 */ }
    }

    private void UpdateHeader(RealtimeQuote? quote)
    {
        if (_currentStock == null) return;
        var src = _dataSource.Name;
        if (quote != null && quote.Last > 0)
        {
            var sign = quote.ChangePct >= 0 ? "+" : "";
            _lblHeader.Text = $"{_currentStock.Code} {_currentStock.Name}   现价 {quote.Last:F2}   {sign}{quote.ChangePct:F2}%   [{src}]";
            _lblHeader.ForeColor = quote.ChangePct >= 0 ? Color.Crimson : Color.LimeGreen;
        }
        else
        {
            _lblHeader.Text = $"{_currentStock.Code} {_currentStock.Name}   [{src}]";
            _lblHeader.ForeColor = _theme.WinFore;
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
            Code = _currentStock.Code, Name = _currentStock.Name,
            FullCode = _currentStock.FullCode, Market = _currentStock.Market,
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
        var crossSummary = RenderMacdChart();
        RenderKdjChart();
        var chip = ChipDistribution.Calculate(_currentBars);
        RenderChipChart(chip);

        var report = SentimentAnalyzer.Analyze(_currentBars, chip);
        var sb = new StringBuilder(SentimentAnalyzer.Format(report));
        sb.AppendLine();
        sb.Append(crossSummary);
        _txtReport.Text = sb.ToString();

        _txtExplain.Text = IndicatorExplainer.Format(IndicatorExplainer.Explain(_currentBars, chip));

        var risk = RiskMetrics.Calculate(_currentBars);
        _txtRisk.Text = RiskMetrics.Format(risk, _currentBars[^1].Close);
    }

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
        cs.RisingFillStyle.Color = _theme.RedUp;
        cs.FallingFillStyle.Color = _theme.GreenDown;
        cs.RisingLineStyle.Color = _theme.RedUp;
        cs.FallingLineStyle.Color = _theme.GreenDown;

        var closes = bars.Select(b => b.Close).ToArray();
        var dates = bars.Select(b => b.Date.ToOADate()).ToArray();

        AddMa(p, dates, closes, 5, ScottPlot.Color.FromHex("#F59F00"));
        AddMa(p, dates, closes, 10, ScottPlot.Color.FromHex("#1971C2"));
        AddMa(p, dates, closes, 20, ScottPlot.Color.FromHex("#7048E8"));
        AddMa(p, dates, closes, 60, ScottPlot.Color.FromHex("#0CA678"));

        var boll = Indicators.Indicators.BOLL(closes);
        AddLine(p, dates, boll.Upper, ScottPlot.Color.FromHex("#868E96"), "BOLL上");
        AddLine(p, dates, boll.Mid, ScottPlot.Color.FromHex("#495057"), "BOLL中");
        AddLine(p, dates, boll.Lower, ScottPlot.Color.FromHex("#868E96"), "BOLL下");

        // 形态识别 → 标注
        var hits = Patterns.Detect(bars);
        foreach (var h in hits)
        {
            double y = h.Bullish ? bars[h.Index].Low * 0.985 : bars[h.Index].High * 1.015;
            var marker = p.Add.Marker(dates[h.Index], y);
            marker.MarkerStyle.Shape = h.Bullish ? MarkerShape.FilledTriangleUp : MarkerShape.FilledTriangleDown;
            marker.MarkerStyle.Size = 12;
            marker.MarkerStyle.FillColor = h.Bullish ? _theme.RedUp.WithAlpha(0.85) : _theme.GreenDown.WithAlpha(0.85);
            marker.MarkerStyle.LineColor = h.Bullish ? _theme.RedUp : _theme.GreenDown;
        }

        // 十字光标 + 浮窗（默认隐藏）
        _crosshair = p.Add.Crosshair(0, 0);
        _crosshair.LineWidth = 1;
        _crosshair.LineColor = _theme.Axis.WithAlpha(0.5);
        _crosshair.IsVisible = false;

        _hoverNote = p.Add.Annotation("", Alignment.UpperRight);
        _hoverNote.LabelFontSize = 11;
        _hoverNote.LabelBackgroundColor = _theme.DataBack.WithAlpha(0.92);
        _hoverNote.LabelFontColor = _theme.Axis;
        _hoverNote.LabelBorderColor = _theme.Grid;
        _hoverNote.IsVisible = false;

        p.Title($"{_currentStock!.Code} {_currentStock.Name}  K线 / 均线 / BOLL  ({hits.Count} 个形态)");
        p.Axes.DateTimeTicksBottom();
        p.ShowLegend(Alignment.UpperLeft);
        Themes.ApplyToPlot(p, _theme);
        _plotPrice.Refresh();
    }

    private static void AddMa(Plot p, double[] dates, double[] closes, int period, ScottPlot.Color color)
    {
        var ma = Indicators.Indicators.SMA(closes, period);
        var xs = new List<double>(); var ys = new List<double>();
        for (int i = 0; i < ma.Length; i++)
        {
            if (double.IsNaN(ma[i])) continue;
            xs.Add(dates[i]); ys.Add(ma[i]);
        }
        if (xs.Count == 0) return;
        var sc = p.Add.Scatter(xs.ToArray(), ys.ToArray());
        sc.LineWidth = 1.4f; sc.MarkerSize = 0; sc.Color = color;
        sc.LegendText = $"MA{period}";
    }

    private static void AddLine(Plot p, double[] dates, double[] vals, ScottPlot.Color color, string label)
    {
        var xs = new List<double>(); var ys = new List<double>();
        for (int i = 0; i < vals.Length; i++)
        {
            if (double.IsNaN(vals[i])) continue;
            xs.Add(dates[i]); ys.Add(vals[i]);
        }
        if (xs.Count == 0) return;
        var sc = p.Add.Scatter(xs.ToArray(), ys.ToArray());
        sc.LineWidth = 1f; sc.MarkerSize = 0; sc.Color = color;
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
                FillColor = up ? _theme.RedUp : _theme.GreenDown,
                LineColor = up ? _theme.RedUp : _theme.GreenDown,
                Size = 0.7,
            });
        }
        p.Add.Bars(blist);
        p.Title("成交量");
        p.Axes.DateTimeTicksBottom();
        Themes.ApplyToPlot(p, _theme);
        _plotVolume.Refresh();
    }

    /// <summary>渲染 MACD 并返回金/死叉历史统计文本。</summary>
    private string RenderMacdChart()
    {
        var p = _plotMacd.Plot;
        p.Clear();

        var bars = _currentBars;
        var closes = bars.Select(b => b.Close).ToArray();
        var dates = bars.Select(b => b.Date.ToOADate()).ToArray();
        var macd = Indicators.Indicators.MACD(closes);

        var blist = new List<Bar>();
        for (int i = 0; i < macd.Hist.Length; i++)
        {
            blist.Add(new Bar
            {
                Position = dates[i],
                Value = macd.Hist[i],
                FillColor = macd.Hist[i] >= 0 ? _theme.RedUp : _theme.GreenDown,
                LineColor = macd.Hist[i] >= 0 ? _theme.RedUp : _theme.GreenDown,
                Size = 0.6,
            });
        }
        p.Add.Bars(blist);

        var difLine = p.Add.Scatter(dates, macd.Dif);
        difLine.MarkerSize = 0; difLine.LineWidth = 1.3f;
        difLine.Color = ScottPlot.Color.FromHex("#1971C2"); difLine.LegendText = "DIF";
        var deaLine = p.Add.Scatter(dates, macd.Dea);
        deaLine.MarkerSize = 0; deaLine.LineWidth = 1.3f;
        deaLine.Color = ScottPlot.Color.FromHex("#F59F00"); deaLine.LegendText = "DEA";

        // 检测金死叉并标注 + 统计次日表现
        int gold = 0, dead = 0;
        double goldRet = 0, deadRet = 0;
        int goldWin = 0, deadWin = 0;
        for (int i = 1; i < macd.Hist.Length; i++)
        {
            bool g = macd.Hist[i] > 0 && macd.Hist[i - 1] <= 0;
            bool d = macd.Hist[i] < 0 && macd.Hist[i - 1] >= 0;
            if (!g && !d) continue;
            double y = g ? Math.Min(macd.Dif[i], macd.Dea[i]) - 0.02 : Math.Max(macd.Dif[i], macd.Dea[i]) + 0.02;
            var m = p.Add.Marker(dates[i], y);
            m.MarkerStyle.Shape = g ? MarkerShape.FilledTriangleUp : MarkerShape.FilledTriangleDown;
            m.MarkerStyle.Size = 10;
            m.MarkerStyle.FillColor = g ? _theme.RedUp : _theme.GreenDown;
            m.MarkerStyle.LineColor = g ? _theme.RedUp : _theme.GreenDown;

            if (i + 1 < bars.Count)
            {
                double r = bars[i + 1].Close / bars[i].Close - 1.0;
                if (g) { gold++; goldRet += r; if (r > 0) goldWin++; }
                else { dead++; deadRet += r; if (r < 0) deadWin++; }
            }
        }

        p.Title("MACD (12,26,9)");
        p.Axes.DateTimeTicksBottom();
        p.ShowLegend(Alignment.UpperLeft);
        Themes.ApplyToPlot(p, _theme);
        _plotMacd.Refresh();

        var sb = new StringBuilder();
        sb.AppendLine("—— MACD 历史信号统计（次日表现）——");
        if (gold > 0)
            sb.AppendLine($"  • 金叉 ▲ 共 {gold} 次，次日平均涨幅 {goldRet / gold:P2}，胜率 {(double)goldWin / gold:P0}");
        else sb.AppendLine("  • 金叉 ▲ 区间内未出现");
        if (dead > 0)
            sb.AppendLine($"  • 死叉 ▼ 共 {dead} 次，次日平均涨幅 {deadRet / dead:P2}，下跌占比 {(double)deadWin / dead:P0}");
        else sb.AppendLine("  • 死叉 ▼ 区间内未出现");
        return sb.ToString();
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

        var lr = p.Add.Scatter(dates, rsi); lr.MarkerSize = 0; lr.LineWidth = 1.0f;
        lr.Color = ScottPlot.Color.FromHex("#7048E8"); lr.LegendText = "RSI14";
        lr.LineStyle.Pattern = LinePattern.Dashed;

        // KDJ 金叉 / 死叉
        for (int i = 1; i < kdj.K.Length; i++)
        {
            bool g = kdj.K[i] > kdj.D[i] && kdj.K[i - 1] <= kdj.D[i - 1];
            bool d = kdj.K[i] < kdj.D[i] && kdj.K[i - 1] >= kdj.D[i - 1];
            if (!g && !d) continue;
            double y = g ? kdj.K[i] - 4 : kdj.K[i] + 4;
            var m = p.Add.Marker(dates[i], y);
            m.MarkerStyle.Shape = g ? MarkerShape.FilledTriangleUp : MarkerShape.FilledTriangleDown;
            m.MarkerStyle.Size = 8;
            m.MarkerStyle.FillColor = g ? _theme.RedUp : _theme.GreenDown;
            m.MarkerStyle.LineColor = g ? _theme.RedUp : _theme.GreenDown;
        }

        var hl1 = p.Add.HorizontalLine(80); hl1.Color = ScottPlot.Color.FromHex("#ADB5BD"); hl1.LinePattern = LinePattern.Dotted;
        var hl2 = p.Add.HorizontalLine(20); hl2.Color = ScottPlot.Color.FromHex("#ADB5BD"); hl2.LinePattern = LinePattern.Dotted;

        p.Title("KDJ (9,3,3) / RSI(14)");
        p.Axes.DateTimeTicksBottom();
        p.ShowLegend(Alignment.UpperLeft);
        Themes.ApplyToPlot(p, _theme);
        _plotKdj.Refresh();
    }

    private void RenderChipChart(ChipDistribution.Result chip)
    {
        var p = _plotChip.Plot;
        p.Clear();
        if (chip.Prices.Length == 0) { _plotChip.Refresh(); return; }
        double maxChip = chip.Chips.Max();
        if (maxChip <= 0) { _plotChip.Refresh(); return; }

        double step = chip.Prices.Length > 1 ? chip.Prices[1] - chip.Prices[0] : 0.01;
        var blist = new List<Bar>();
        for (int i = 0; i < chip.Prices.Length; i++)
        {
            blist.Add(new Bar
            {
                Position = chip.Prices[i],
                Value = chip.Chips[i] / maxChip,
                FillColor = ScottPlot.Color.FromHex("#4DABF7"),
                LineColor = ScottPlot.Color.FromHex("#4DABF7"),
                Size = step * 0.9,
                Orientation = ScottPlot.Orientation.Horizontal,
            });
        }
        p.Add.Bars(blist);

        double cur = _currentBars[^1].Close;
        var hlCur = p.Add.HorizontalLine(cur);
        hlCur.Color = _theme.RedUp; hlCur.LineWidth = 2;
        hlCur.Text = $"现价 {cur:F2}"; hlCur.LabelOppositeAxis = true;

        var hlAvg = p.Add.HorizontalLine(chip.AvgCost);
        hlAvg.Color = ScottPlot.Color.FromHex("#7048E8"); hlAvg.LineWidth = 2;
        hlAvg.LinePattern = LinePattern.Dashed;
        hlAvg.Text = $"均成本 {chip.AvgCost:F2}"; hlAvg.LabelOppositeAxis = true;

        p.Title($"筹码分布   获利盘 {chip.ProfitRatio:P1}   70%集中度 {chip.Concentration70:P1}");
        p.XLabel("筹码相对密度");
        p.YLabel("价格");
        Themes.ApplyToPlot(p, _theme);
        _plotChip.Refresh();
    }

    // ============================================================
    // 十字光标
    // ============================================================
    private void OnPriceMouseMove(object? sender, MouseEventArgs e)
    {
        if (_currentBars.Count == 0 || _crosshair == null || _hoverNote == null) return;
        Pixel mp = new(e.X, e.Y);
        Coordinates c = _plotPrice.Plot.GetCoordinates(mp);

        // 找最近的 K 线
        int idx = -1;
        double bestDist = double.MaxValue;
        for (int i = 0; i < _currentBars.Count; i++)
        {
            double d = Math.Abs(_currentBars[i].Date.ToOADate() - c.X);
            if (d < bestDist) { bestDist = d; idx = i; }
        }
        if (idx < 0) return;
        var b = _currentBars[idx];
        double prev = idx > 0 ? _currentBars[idx - 1].Close : b.Open;
        double chg = (b.Close - prev) / prev * 100;
        string sign = chg >= 0 ? "+" : "";

        _crosshair.IsVisible = true;
        _crosshair.Position = new Coordinates(b.Date.ToOADate(), b.Close);

        _hoverNote.IsVisible = true;
        _hoverNote.Text =
            $"{b.Date:yyyy-MM-dd}\n" +
            $"开 {b.Open:F2}  高 {b.High:F2}\n" +
            $"低 {b.Low:F2}  收 {b.Close:F2}\n" +
            $"涨跌 {sign}{chg:F2}%\n" +
            $"成交量 {b.Volume / 10000:F1} 万手";
        _plotPrice.Refresh();
    }

    private void HideCrosshair()
    {
        if (_crosshair != null) _crosshair.IsVisible = false;
        if (_hoverNote != null) _hoverNote.IsVisible = false;
        _plotPrice.Refresh();
    }

    // ============================================================
    // 多图 X 轴联动（缩放/平移其中一个，其他三个跟随）
    // ============================================================
    private bool _syncing;
    private void WireAxisSync()
    {
        var plots = new[] { _plotPrice, _plotVolume, _plotMacd, _plotKdj };
        foreach (var fp in plots)
        {
            fp.MouseUp += (_, _) => SyncFrom(fp);
            fp.MouseWheel += (_, _) => SyncFrom(fp);
            fp.MouseMove += (_, e) =>
            {
                if (e.Button == MouseButtons.Left || e.Button == MouseButtons.Middle || e.Button == MouseButtons.Right)
                    SyncFrom(fp);
            };
            fp.DoubleClick += (_, _) => SyncFrom(fp);
        }
    }

    private void SyncFrom(FormsPlot src)
    {
        if (_syncing) return;
        _syncing = true;
        try
        {
            var x = src.Plot.Axes.GetLimits().HorizontalRange;
            foreach (var fp in new[] { _plotPrice, _plotVolume, _plotMacd, _plotKdj })
            {
                if (ReferenceEquals(fp, src)) continue;
                fp.Plot.Axes.SetLimitsX(x.Min, x.Max);
                fp.Refresh();
            }
        }
        finally { _syncing = false; }
    }

    /// <summary>关闭某个 FormsPlot 的所有内置鼠标交互（缩放/拖拽/双击重置）。</summary>
    private static void DisableInteraction(FormsPlot fp)
    {
        fp.UserInputProcessor.Disable();
    }

    // ============================================================
    // 导出
    // ============================================================
    private void ShowExportMenu()
    {
        var menu = new ContextMenuStrip();
        menu.Items.Add("导出 K 线图 PNG", null, (_, _) => ExportPng(_plotPrice, "kline"));
        menu.Items.Add("导出 全部图表 PNG (拼图)", null, (_, _) => ExportAllPng());
        menu.Items.Add("导出 K 线 CSV", null, (_, _) => ExportCsv());
        menu.Items.Add("导出 分析报告 Markdown", null, (_, _) => ExportMarkdown());
        menu.Show(_btnExport, new Point(0, _btnExport.Height));
    }

    private void ExportPng(FormsPlot fp, string suffix)
    {
        if (_currentStock == null) return;
        using var sfd = new SaveFileDialog
        {
            Filter = "PNG (*.png)|*.png",
            FileName = $"{_currentStock.Code}_{_currentStock.Name}_{suffix}_{DateTime.Now:yyyyMMddHHmmss}.png",
        };
        if (sfd.ShowDialog(this) != DialogResult.OK) return;
        fp.Plot.SavePng(sfd.FileName, fp.Width, fp.Height);
        MessageBox.Show(this, "已导出：" + sfd.FileName, "导出完成");
    }

    private void ExportAllPng()
    {
        if (_currentStock == null) return;
        using var sfd = new SaveFileDialog
        {
            Filter = "PNG (*.png)|*.png",
            FileName = $"{_currentStock.Code}_{_currentStock.Name}_full_{DateTime.Now:yyyyMMddHHmmss}.png",
        };
        if (sfd.ShowDialog(this) != DialogResult.OK) return;

        // 简单方案：分别保存 4 张然后拼接
        int W = 1400, H = 900;
        using var bmp = new Bitmap(W, H);
        using var g = Graphics.FromImage(bmp);
        g.Clear(_theme.WinBack);

        DrawPlotInto(g, _plotPrice, 0, 0, W, (int)(H * 0.5));
        DrawPlotInto(g, _plotVolume, 0, (int)(H * 0.5), W, (int)(H * 0.16));
        DrawPlotInto(g, _plotMacd, 0, (int)(H * 0.66), W, (int)(H * 0.17));
        DrawPlotInto(g, _plotKdj, 0, (int)(H * 0.83), W, (int)(H * 0.17));
        bmp.Save(sfd.FileName, System.Drawing.Imaging.ImageFormat.Png);
        MessageBox.Show(this, "已导出：" + sfd.FileName, "导出完成");
    }

    private static void DrawPlotInto(Graphics g, FormsPlot fp, int x, int y, int w, int h)
    {
        var img = fp.Plot.GetImage(w, h);
        var bytes = img.GetImageBytes();
        using var ms = new MemoryStream(bytes);
        using var bmp = new Bitmap(ms);
        g.DrawImage(bmp, x, y, w, h);
    }

    private void ExportCsv()
    {
        if (_currentStock == null || _currentBars.Count == 0) return;
        using var sfd = new SaveFileDialog
        {
            Filter = "CSV (*.csv)|*.csv",
            FileName = $"{_currentStock.Code}_{_currentStock.Name}_{DateTime.Now:yyyyMMdd}.csv",
        };
        if (sfd.ShowDialog(this) != DialogResult.OK) return;
        using var sw = new StreamWriter(sfd.FileName, false, new UTF8Encoding(true));
        sw.WriteLine("date,open,high,low,close,volume,amount");
        var ci = CultureInfo.InvariantCulture;
        foreach (var b in _currentBars)
            sw.WriteLine($"{b.Date:yyyy-MM-dd},{b.Open.ToString(ci)},{b.High.ToString(ci)},{b.Low.ToString(ci)},{b.Close.ToString(ci)},{b.Volume.ToString(ci)},{b.Amount.ToString(ci)}");
        MessageBox.Show(this, "已导出：" + sfd.FileName, "导出完成");
    }

    private void ExportMarkdown()
    {
        if (_currentStock == null || _currentBars.Count == 0) return;
        using var sfd = new SaveFileDialog
        {
            Filter = "Markdown (*.md)|*.md",
            FileName = $"{_currentStock.Code}_{_currentStock.Name}_report_{DateTime.Now:yyyyMMdd}.md",
        };
        if (sfd.ShowDialog(this) != DialogResult.OK) return;

        var sb = new StringBuilder();
        sb.AppendLine($"# {_currentStock.Code} {_currentStock.Name} 技术分析报告");
        sb.AppendLine();
        sb.AppendLine($"- 生成时间：{DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine($"- 数据源：{_dataSource.Name}");
        sb.AppendLine($"- 周期：{_cboPeriod.SelectedItem}    样本数：{_currentBars.Count}");
        sb.AppendLine($"- 区间：{_currentBars[0].Date:yyyy-MM-dd} ~ {_currentBars[^1].Date:yyyy-MM-dd}");
        sb.AppendLine();
        sb.AppendLine("## 综合分析");
        sb.AppendLine("```");
        sb.AppendLine(_txtReport.Text);
        sb.AppendLine("```");
        sb.AppendLine("## 指标解读");
        sb.AppendLine("```");
        sb.AppendLine(_txtExplain.Text);
        sb.AppendLine("```");
        sb.AppendLine("## 基本面 + 资金流向");
        sb.AppendLine("```");
        sb.AppendLine(_txtFundFlow.Text);
        sb.AppendLine("```");
        sb.AppendLine("## 风险指标");
        sb.AppendLine("```");
        sb.AppendLine(_txtRisk.Text);
        sb.AppendLine("```");
        sb.AppendLine();
        sb.AppendLine("> ⚠️ 本报告仅供学习研究，不构成任何投资建议。");
        File.WriteAllText(sfd.FileName, sb.ToString(), new UTF8Encoding(true));
        MessageBox.Show(this, "已导出：" + sfd.FileName, "导出完成");
    }

    // ============================================================
    // 画图工具菜单
    // ============================================================
    private void ShowDrawMenu()
    {
        EnsureDrawingTool();
        var menu = new ContextMenuStrip();
        menu.Items.Add("无 (浏览模式)", null, (_, _) => _drawingTool!.SetMode(DrawingTool.Mode.None));
        menu.Items.Add("趋势线", null, (_, _) => _drawingTool!.SetMode(DrawingTool.Mode.Trendline));
        menu.Items.Add("水平价位线", null, (_, _) => _drawingTool!.SetMode(DrawingTool.Mode.Horizontal));
        menu.Items.Add("斐波那契回撤", null, (_, _) => _drawingTool!.SetMode(DrawingTool.Mode.Fibonacci));
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("清除全部", null, (_, _) => { _drawingTool!.ClearAll(); _plotPrice.Refresh(); });
        menu.Show(_btnDraw, new Point(0, _btnDraw.Height));
    }

    private void EnsureDrawingTool()
    {
        if (_drawingTool == null) _drawingTool = new DrawingTool(_plotPrice);
    }

    // ============================================================
    // 工具菜单
    // ============================================================
    private void ShowToolsMenu()
    {
        var menu = new ContextMenuStrip();
        menu.Items.Add("分时图...", null, (_, _) => OpenMinute());
        menu.Items.Add("多周期对比...", null, (_, _) => OpenMultiPeriod());
        menu.Items.Add("个股 / 大盘对比...", null, (_, _) => OpenCompare());
        menu.Items.Add("策略回测...", null, (_, _) => OpenBacktest());
        menu.Items.Add("AI 短线方向预测...", null, (_, _) => OpenPredict());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("自选股看板...", null, (_, _) => new WatchlistDashboardForm(
            _settings.Watchlist, _dataSource, _sina, _theme, async s =>
            {
                _currentStock = s;
                await ReloadAsync();
            }).Show(this));
        menu.Items.Add("市场宽度 (龙虎榜/北向/板块)...", null, (_, _) => new MarketBoardForm(_theme).Show(this));
        menu.Items.Add("可转债 / ETF...", null, (_, _) => new BondEtfForm(_theme).Show(this));
        menu.Show(_btnTools, new Point(0, _btnTools.Height));
    }

    private void OpenMinute()
    {
        if (_currentStock == null) { MessageBox.Show(this, "请先选择股票", "提示"); return; }
        new MinuteForm(_currentStock, _theme).Show(this);
    }

    private void OpenMultiPeriod()
    {
        if (_currentStock == null) { MessageBox.Show(this, "请先选择股票", "提示"); return; }
        new MultiPeriodForm(_currentStock, _dataSource, _theme).Show(this);
    }

    private void OpenCompare()
    {
        string? def = _currentStock != null ? $"sh000001,{_currentStock.FullCode}" : null;
        new CompareForm(_dataSource, _sina, _theme, def).Show(this);
    }

    private void OpenBacktest()
    {
        if (_currentStock == null || _currentBars.Count < 60)
        { MessageBox.Show(this, "请先加载至少 60 根 K 线", "提示"); return; }
        new BacktestForm(_currentStock, _currentBars, _theme).Show(this);
    }

    private void OpenPredict()
    {
        if (_currentStock == null || _currentBars.Count < 80)
        { MessageBox.Show(this, "请先加载至少 80 根 K 线（建议 200+）", "提示"); return; }
        new PredictForm(_currentStock, _currentBars, _theme).Show(this);
    }
}
