using StockTechAnalyzer.Services;
using Color = System.Drawing.Color;
using Label = System.Windows.Forms.Label;

namespace StockTechAnalyzer.UI;

internal sealed class BondEtfForm : Form
{
    private readonly FundBondDataSource _src = new();
    private readonly Themes.ThemeColors _theme;

    private readonly DataGridView _gridBond;
    private readonly DataGridView _gridEtf;
    private readonly TextBox _txtBondFilter;
    private readonly TextBox _txtEtfFilter;

    private List<FundBondDataSource.BondItem> _bonds = new();
    private List<FundBondDataSource.EtfItem> _etfs = new();

    public BondEtfForm(Themes.ThemeColors theme)
    {
        _theme = theme;
        Text = "可转债 / ETF";
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(1200, 720);

        var tabs = new TabControl { Dock = DockStyle.Fill };

        // ---- 可转债 ----
        var pgB = new TabPage("可转债");
        var topB = new Panel { Dock = DockStyle.Top, Height = 40, BackColor = theme.PanelBack };
        new Label { Text = "搜索：", Left = 10, Top = 12, Width = 50, ForeColor = theme.WinFore, Parent = topB };
        _txtBondFilter = new TextBox { Left = 60, Top = 8, Width = 220, Parent = topB };
        _txtBondFilter.TextChanged += (_, _) => RenderBonds();
        var btnRB = new Button { Text = "刷新", Left = 290, Top = 7, Width = 70, Parent = topB };
        btnRB.Click += async (_, _) => await LoadBondsAsync();
        var lblTipB = new Label
        {
            Left = 380, Top = 12, Width = 760, ForeColor = theme.WinFore, Parent = topB,
            Text = "💡 转股溢价率 < 30% 且价格 < 130 元 通常被视为安全垫较厚的可转债（双低策略）。",
        };

        _gridBond = NewGrid();
        _gridBond.Columns.Add("code", "代码");
        _gridBond.Columns.Add("name", "可转债名称");
        _gridBond.Columns.Add("price", "现价");
        _gridBond.Columns.Add("chg", "涨跌%");
        _gridBond.Columns.Add("premium", "转股溢价%");
        _gridBond.Columns.Add("conv", "转股价");
        _gridBond.Columns.Add("scode", "正股代码");
        _gridBond.Columns.Add("sname", "正股名称");
        _gridBond.Columns.Add("list", "上市日");
        pgB.Controls.Add(_gridBond);
        pgB.Controls.Add(topB);

        // ---- ETF ----
        var pgE = new TabPage("ETF");
        var topE = new Panel { Dock = DockStyle.Top, Height = 40, BackColor = theme.PanelBack };
        new Label { Text = "搜索：", Left = 10, Top = 12, Width = 50, ForeColor = theme.WinFore, Parent = topE };
        _txtEtfFilter = new TextBox { Left = 60, Top = 8, Width = 220, Parent = topE };
        _txtEtfFilter.TextChanged += (_, _) => RenderEtfs();
        var btnRE = new Button { Text = "刷新", Left = 290, Top = 7, Width = 70, Parent = topE };
        btnRE.Click += async (_, _) => await LoadEtfsAsync();
        var lblTipE = new Label
        {
            Left = 380, Top = 12, Width = 760, ForeColor = theme.WinFore, Parent = topE,
            Text = "💡 ETF = 交易所交易基金，可像股票一样买卖一篮子证券，适合定投与行业配置。",
        };

        _gridEtf = NewGrid();
        _gridEtf.Columns.Add("code", "代码");
        _gridEtf.Columns.Add("name", "ETF 名称");
        _gridEtf.Columns.Add("price", "现价");
        _gridEtf.Columns.Add("chg", "涨跌%");
        _gridEtf.Columns.Add("amt", "成交额");
        _gridEtf.Columns.Add("turn", "换手%");
        _gridEtf.Columns.Add("pe", "PE");
        pgE.Controls.Add(_gridEtf);
        pgE.Controls.Add(topE);

        tabs.TabPages.AddRange(new[] { pgB, pgE });
        Controls.Add(tabs);
        Themes.ApplyToControls(this, theme);

        Load += async (_, _) => { await LoadBondsAsync(); await LoadEtfsAsync(); };
    }

    private DataGridView NewGrid() => new()
    {
        Dock = DockStyle.Fill, ReadOnly = true,
        AllowUserToAddRows = false, RowHeadersVisible = false,
        AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
        BackgroundColor = _theme.WinBack, ForeColor = _theme.WinFore,
    };

    private async Task LoadBondsAsync()
    {
        try { _bonds = (await _src.GetConvertibleBondsAsync()).ToList(); RenderBonds(); }
        catch (Exception ex) { MessageBox.Show(this, "可转债加载失败：" + ex.Message, "错误"); }
    }

    private async Task LoadEtfsAsync()
    {
        try { _etfs = (await _src.GetEtfsAsync()).ToList(); RenderEtfs(); }
        catch (Exception ex) { MessageBox.Show(this, "ETF 加载失败：" + ex.Message, "错误"); }
    }

    private void RenderBonds()
    {
        string kw = _txtBondFilter.Text.Trim();
        _gridBond.Rows.Clear();
        foreach (var b in _bonds)
        {
            if (kw.Length > 0 && !b.Code.Contains(kw) && !b.Name.Contains(kw)
                && !b.StockCode.Contains(kw) && !b.StockName.Contains(kw)) continue;
            int row = _gridBond.Rows.Add(b.Code, b.Name,
                b.Price.ToString("F2"),
                (b.ChangePct >= 0 ? "+" : "") + b.ChangePct.ToString("F2"),
                b.Premium.ToString("F2"), b.ConversionPrice.ToString("F2"),
                b.StockCode, b.StockName, b.ListDate);
            _gridBond.Rows[row].Cells["chg"].Style.ForeColor = b.ChangePct >= 0 ? Color.Crimson : Color.LimeGreen;
            // 双低策略高亮
            if (b.Price > 0 && b.Price < 130 && b.Premium < 30)
                _gridBond.Rows[row].DefaultCellStyle.BackColor = Color.FromArgb(_theme == Themes.Dark ? 60 : 255, 240, 200);
        }
    }

    private void RenderEtfs()
    {
        string kw = _txtEtfFilter.Text.Trim();
        _gridEtf.Rows.Clear();
        foreach (var e in _etfs)
        {
            if (kw.Length > 0 && !e.Code.Contains(kw) && !e.Name.Contains(kw)) continue;
            int row = _gridEtf.Rows.Add(e.Code, e.Name,
                e.Price.ToString("F3"),
                (e.ChangePct >= 0 ? "+" : "") + e.ChangePct.ToString("F2"),
                FormatAmount(e.Amount),
                e.TurnoverPct.ToString("F2"),
                e.Pe > 0 ? e.Pe.ToString("F1") : "—");
            _gridEtf.Rows[row].Cells["chg"].Style.ForeColor = e.ChangePct >= 0 ? Color.Crimson : Color.LimeGreen;
        }
    }

    private static string FormatAmount(double a)
    {
        if (a >= 1e8) return (a / 1e8).ToString("F2") + "亿";
        if (a >= 1e4) return (a / 1e4).ToString("F0") + "万";
        return a.ToString("F0");
    }
}
