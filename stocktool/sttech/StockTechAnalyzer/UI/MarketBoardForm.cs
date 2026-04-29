using System.Text;
using ScottPlot;
using ScottPlot.WinForms;
using StockTechAnalyzer.Services;
using Color = System.Drawing.Color;
using FontStyle = System.Drawing.FontStyle;
using Label = System.Windows.Forms.Label;

namespace StockTechAnalyzer.UI;

internal sealed class MarketBoardForm : Form
{
    private readonly EastMoneyMarketData _md = new();
    private readonly Themes.ThemeColors _theme;

    private readonly DataGridView _gridLhb;
    private readonly FormsPlot _plotNorth = new() { Dock = DockStyle.Fill };
    private readonly TextBox _txtNorth;
    private readonly Panel _panelHeat;
    private readonly ComboBox _cboHeatType;

    public MarketBoardForm(Themes.ThemeColors theme)
    {
        _theme = theme;
        Text = "市场宽度 — 龙虎榜 / 北向资金 / 板块热力图";
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(1200, 760);

        var tabs = new TabControl { Dock = DockStyle.Fill };

        // ---- 龙虎榜 ----
        var pgLhb = new TabPage("龙虎榜");
        _gridLhb = new DataGridView
        {
            Dock = DockStyle.Fill, ReadOnly = true,
            AllowUserToAddRows = false, RowHeadersVisible = false,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
        };
        _gridLhb.Columns.Add("date", "日期");
        _gridLhb.Columns.Add("code", "代码");
        _gridLhb.Columns.Add("name", "名称");
        _gridLhb.Columns.Add("chg", "涨跌%");
        _gridLhb.Columns.Add("net", "净买入(万)");
        _gridLhb.Columns.Add("reason", "上榜理由");
        pgLhb.Controls.Add(_gridLhb);

        // ---- 北向资金 ----
        var pgNorth = new TabPage("北向资金");
        _txtNorth = new TextBox
        {
            Dock = DockStyle.Right, Width = 320, Multiline = true, ReadOnly = true,
            ScrollBars = ScrollBars.Vertical, Font = new Font("Consolas", 10f),
        };
        pgNorth.Controls.Add(_plotNorth);
        pgNorth.Controls.Add(_txtNorth);

        // ---- 板块热力图 ----
        var pgHeat = new TabPage("板块热力图");
        var topHeat = new Panel { Dock = DockStyle.Top, Height = 40, BackColor = theme.PanelBack };
        new Label { Text = "板块类型：", Left = 10, Top = 12, Width = 80, ForeColor = theme.WinFore, Parent = topHeat };
        _cboHeatType = new ComboBox { Left = 90, Top = 8, Width = 140, DropDownStyle = ComboBoxStyle.DropDownList, Parent = topHeat };
        _cboHeatType.Items.AddRange(new object[] { "申万行业", "概念板块" });
        _cboHeatType.SelectedIndex = 0;
        var btnRefH = new Button { Text = "刷新", Left = 240, Top = 7, Width = 70, Parent = topHeat };
        btnRefH.Click += async (_, _) => await LoadHeatAsync();
        _panelHeat = new Panel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = theme.WinBack };
        pgHeat.Controls.Add(_panelHeat);
        pgHeat.Controls.Add(topHeat);

        tabs.TabPages.AddRange(new[] { pgLhb, pgNorth, pgHeat });
        Controls.Add(tabs);
        Themes.ApplyToControls(this, theme);
        Themes.ApplyToPlot(_plotNorth.Plot, theme);

        Load += async (_, _) =>
        {
            await LoadLhbAsync();
            await LoadNorthAsync();
            await LoadHeatAsync();
        };

        _panelHeat.Resize += (_, _) => RelayoutHeat();
    }

    private async Task LoadLhbAsync()
    {
        try
        {
            // 取最近一个交易日（向前回溯 7 天）
            for (int back = 0; back < 7; back++)
            {
                var day = DateTime.Today.AddDays(-back);
                if (day.DayOfWeek == DayOfWeek.Saturday || day.DayOfWeek == DayOfWeek.Sunday) continue;
                var list = await _md.GetLhbAsync(day);
                if (list.Count > 0)
                {
                    _gridLhb.Rows.Clear();
                    foreach (var l in list)
                    {
                        int row = _gridLhb.Rows.Add(l.Date, l.Code, l.Name,
                            (l.ChangePct >= 0 ? "+" : "") + l.ChangePct.ToString("F2"),
                            l.NetBuy.ToString("F0"), l.Reason);
                        var col = l.NetBuy >= 0 ? Color.Crimson : Color.LimeGreen;
                        _gridLhb.Rows[row].Cells["chg"].Style.ForeColor = l.ChangePct >= 0 ? Color.Crimson : Color.LimeGreen;
                        _gridLhb.Rows[row].Cells["net"].Style.ForeColor = col;
                    }
                    Text = $"市场宽度 — 龙虎榜 ({l_dateStr(list[0].Date)})";
                    return;
                }
            }
        }
        catch (Exception ex) { MessageBox.Show(this, "龙虎榜加载失败：" + ex.Message, "错误"); }
    }

    private static string l_dateStr(string s) => s;

    private async Task LoadNorthAsync()
    {
        try
        {
            var data = await _md.GetNorthFlowAsync(60);
            if (data.Count == 0) return;

            var p = _plotNorth.Plot; p.Clear();
            var xs = data.Select(d => d.Date.ToOADate()).ToArray();
            var ys = data.Select(d => d.TotalNet).ToArray();
            var bars = new List<Bar>();
            for (int i = 0; i < data.Count; i++)
            {
                bars.Add(new Bar
                {
                    Position = xs[i],
                    Value = ys[i],
                    FillColor = ys[i] >= 0 ? _theme.RedUp : _theme.GreenDown,
                    LineColor = ys[i] >= 0 ? _theme.RedUp : _theme.GreenDown,
                    Size = 0.7,
                });
            }
            p.Add.Bars(bars);
            p.Title("北向资金日净流入 (亿元)");
            p.Axes.DateTimeTicksBottom();
            Themes.ApplyToPlot(p, _theme);
            _plotNorth.Refresh();

            var sb = new StringBuilder();
            sb.AppendLine("最近 10 个交易日：");
            sb.AppendLine($"{"日期",-12}{"沪股通",10}{"深股通",10}{"合计",10}");
            foreach (var d in data.TakeLast(10).Reverse())
                sb.AppendLine($"{d.Date:yyyy-MM-dd}{d.HuGuTongNet,10:F2}{d.ShenGuTongNet,10:F2}{d.TotalNet,10:F2}");
            sb.AppendLine();
            double sum10 = data.TakeLast(10).Sum(d => d.TotalNet);
            double sum30 = data.TakeLast(30).Sum(d => d.TotalNet);
            sb.AppendLine($"近 10 日累计：{sum10:F2} 亿");
            sb.AppendLine($"近 30 日累计：{sum30:F2} 亿");
            sb.AppendLine();
            sb.AppendLine("解读：北向资金常被视为外资风向标，持续净流入通常对应 A 股偏强氛围。");
            _txtNorth.Text = sb.ToString();
        }
        catch (Exception ex) { _txtNorth.Text = "加载失败：" + ex.Message; }
    }

    private List<EastMoneyMarketData.SectorItem> _heatData = new();

    private async Task LoadHeatAsync()
    {
        try
        {
            var list = await _md.GetSectorsAsync(concept: _cboHeatType.SelectedIndex == 1);
            _heatData = list.OrderByDescending(s => s.ChangePct).ToList();
            RelayoutHeat();
        }
        catch (Exception ex) { MessageBox.Show(this, "板块加载失败：" + ex.Message, "错误"); }
    }

    private void RelayoutHeat()
    {
        _panelHeat.SuspendLayout();
        _panelHeat.Controls.Clear();
        if (_heatData.Count == 0) { _panelHeat.ResumeLayout(); return; }

        int cols = Math.Max(4, _panelHeat.Width / 160);
        int cellW = (_panelHeat.Width - 10) / cols - 4;
        int cellH = 60;
        for (int i = 0; i < _heatData.Count; i++)
        {
            var s = _heatData[i];
            int row = i / cols, col = i % cols;
            var lbl = new Label
            {
                Left = col * (cellW + 4) + 5, Top = row * (cellH + 4) + 5,
                Width = cellW, Height = cellH,
                Text = $"{s.Name}\n{(s.ChangePct >= 0 ? "+" : "")}{s.ChangePct:F2}%",
                TextAlign = ContentAlignment.MiddleCenter,
                BackColor = HeatColor(s.ChangePct),
                ForeColor = Color.White,
                Font = new Font("Microsoft YaHei UI", 10f, FontStyle.Bold),
                BorderStyle = BorderStyle.FixedSingle,
            };
            _panelHeat.Controls.Add(lbl);
        }
        _panelHeat.ResumeLayout();
    }

    private static Color HeatColor(double pct)
    {
        // -5% 深绿 → 0 灰 → +5% 深红
        double t = Math.Max(-5, Math.Min(5, pct)) / 5.0;
        if (t >= 0)
        {
            int r = (int)(120 + 130 * t);
            int g = (int)(120 - 80 * t);
            int b = (int)(120 - 80 * t);
            return Color.FromArgb(r, g, b);
        }
        else
        {
            double tt = -t;
            int r = (int)(120 - 80 * tt);
            int g = (int)(120 + 80 * tt);
            int b = (int)(120 - 80 * tt);
            return Color.FromArgb(r, g, b);
        }
    }
}
