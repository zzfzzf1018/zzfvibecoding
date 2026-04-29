using StockTechAnalyzer.Indicators;
using StockTechAnalyzer.Models;
using StockTechAnalyzer.Services;
using Color = System.Drawing.Color;

namespace StockTechAnalyzer.UI;

/// <summary>
/// 自选股看板：表格展示价、涨跌、MACD/KDJ/RSI 信号；定时刷新。
/// </summary>
internal sealed class WatchlistDashboardForm : Form
{
    private readonly IStockDataSource _src;
    private readonly SinaStockDataSource _sina;
    private readonly Themes.ThemeColors _theme;
    private readonly IList<StockInfo> _watch;
    private readonly DataGridView _grid;
    private readonly System.Windows.Forms.Timer _timer;
    private readonly Button _btnRefresh;
    private readonly Label _lblStatus;
    private readonly Action<StockInfo> _onSelect;

    public WatchlistDashboardForm(IList<StockInfo> watch, IStockDataSource src, SinaStockDataSource sina,
        Themes.ThemeColors theme, Action<StockInfo> onSelect)
    {
        _watch = watch; _src = src; _sina = sina; _theme = theme; _onSelect = onSelect;
        Text = "自选股看板";
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(1000, 600);

        var top = new Panel { Dock = DockStyle.Top, Height = 40, BackColor = theme.PanelBack };
        _btnRefresh = new Button { Text = "立即刷新", Left = 10, Top = 8, Width = 90, Parent = top };
        _btnRefresh.Click += async (_, _) => await RefreshAllAsync();
        _lblStatus = new Label { Left = 110, Top = 12, Width = 600, ForeColor = theme.WinFore, Parent = top };

        _grid = new DataGridView
        {
            Dock = DockStyle.Fill, ReadOnly = true,
            AllowUserToAddRows = false, RowHeadersVisible = false,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            BackgroundColor = theme.WinBack, ForeColor = theme.WinFore,
        };
        _grid.Columns.Add("code", "代码");
        _grid.Columns.Add("name", "名称");
        _grid.Columns.Add("price", "现价");
        _grid.Columns.Add("chg", "涨跌%");
        _grid.Columns.Add("macd", "MACD");
        _grid.Columns.Add("kdj", "KDJ");
        _grid.Columns.Add("rsi", "RSI");
        _grid.Columns.Add("trend", "趋势");
        _grid.CellDoubleClick += (s, e) =>
        {
            if (e.RowIndex < 0 || e.RowIndex >= _watch.Count) return;
            _onSelect(_watch[e.RowIndex]);
        };

        Controls.Add(_grid);
        Controls.Add(top);
        Themes.ApplyToControls(this, theme);

        _timer = new System.Windows.Forms.Timer { Interval = 30000 };
        _timer.Tick += async (_, _) => await RefreshAllAsync();
        Load += async (_, _) => { await RefreshAllAsync(); _timer.Start(); };
        FormClosing += (_, _) => _timer.Stop();
    }

    private async Task RefreshAllAsync()
    {
        if (_watch.Count == 0) { _lblStatus.Text = "（自选股为空，请先在主窗口添加）"; return; }
        _lblStatus.Text = $"刷新中...  {DateTime.Now:HH:mm:ss}";
        _btnRefresh.Enabled = false;
        try
        {
            _grid.Rows.Clear();
            foreach (var s in _watch)
            {
                var quote = await _sina.GetQuoteAsync(s);
                IReadOnlyList<Kline> bars;
                try { bars = await _src.GetKlineAsync(s, KlinePeriod.Daily, 80); }
                catch { bars = Array.Empty<Kline>(); }

                string macdSig = "—", kdjSig = "—", rsiSig = "—", trendSig = "—";
                if (bars.Count >= 35)
                {
                    var closes = bars.Select(b => b.Close).ToArray();
                    var m = Indicators.Indicators.MACD(closes);
                    var k = Indicators.Indicators.KDJ(bars);
                    var r = Indicators.Indicators.RSI(closes);
                    macdSig = m.Hist[^1] > 0 && m.Hist[^2] <= 0 ? "金叉▲" :
                              m.Hist[^1] < 0 && m.Hist[^2] >= 0 ? "死叉▼" :
                              m.Hist[^1] > 0 ? "多" : "空";
                    kdjSig = k.K[^1] > k.D[^1] && k.K[^2] <= k.D[^2] ? "金叉▲" :
                             k.K[^1] < k.D[^1] && k.K[^2] >= k.D[^2] ? "死叉▼" :
                             k.J[^1] > 80 ? "超买" : k.J[^1] < 20 ? "超卖" : "中性";
                    rsiSig = r[^1] > 70 ? $"超买{r[^1]:F0}" : r[^1] < 30 ? $"超卖{r[^1]:F0}" : $"{r[^1]:F0}";
                    var ma5 = Indicators.Indicators.SMA(closes, 5);
                    var ma20 = Indicators.Indicators.SMA(closes, 20);
                    var ma60 = Indicators.Indicators.SMA(closes, 60);
                    if (ma5[^1] > ma20[^1] && ma20[^1] > ma60[^1]) trendSig = "多头排列";
                    else if (ma5[^1] < ma20[^1] && ma20[^1] < ma60[^1]) trendSig = "空头排列";
                    else trendSig = "震荡";
                }

                double price = quote?.Last ?? (bars.Count > 0 ? bars[^1].Close : 0);
                double chg = quote?.ChangePct ?? 0;
                int row = _grid.Rows.Add(s.Code, s.Name, price.ToString("F2"),
                    (chg >= 0 ? "+" : "") + chg.ToString("F2"),
                    macdSig, kdjSig, rsiSig, trendSig);

                var col = chg >= 0 ? Color.Crimson : Color.LimeGreen;
                _grid.Rows[row].Cells["price"].Style.ForeColor = col;
                _grid.Rows[row].Cells["chg"].Style.ForeColor = col;
                if (macdSig.Contains("金")) _grid.Rows[row].Cells["macd"].Style.ForeColor = Color.Crimson;
                else if (macdSig.Contains("死")) _grid.Rows[row].Cells["macd"].Style.ForeColor = Color.LimeGreen;
                if (kdjSig.Contains("金")) _grid.Rows[row].Cells["kdj"].Style.ForeColor = Color.Crimson;
                else if (kdjSig.Contains("死")) _grid.Rows[row].Cells["kdj"].Style.ForeColor = Color.LimeGreen;
            }
            _lblStatus.Text = $"已刷新  {DateTime.Now:HH:mm:ss}    双击行可在主窗口加载该股票";
        }
        catch (Exception ex) { _lblStatus.Text = "刷新出错：" + ex.Message; }
        finally { _btnRefresh.Enabled = true; }
    }
}
