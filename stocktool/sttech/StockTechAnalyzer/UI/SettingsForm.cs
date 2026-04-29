using StockTechAnalyzer.Storage;

namespace StockTechAnalyzer.UI;

/// <summary>设置对话框：数据源 / Tushare Token / 主题 / 缓存。</summary>
internal sealed class SettingsForm : Form
{
    private readonly ComboBox _cboSource;
    private readonly TextBox _txtToken;
    private readonly CheckBox _chkDark;
    private readonly CheckBox _chkCache;

    public string DataSource => (string)_cboSource.SelectedItem!;
    public string TushareToken => _txtToken.Text.Trim();
    public bool DarkMode => _chkDark.Checked;
    public bool EnableCache => _chkCache.Checked;

    public SettingsForm(AppSettings settings)
    {
        Text = "设置";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false; MinimizeBox = false;
        ClientSize = new Size(460, 280);

        var lblSrc = new Label { Text = "数据源：", Left = 20, Top = 24, Width = 80 };
        _cboSource = new ComboBox
        {
            Left = 100, Top = 20, Width = 320,
            DropDownStyle = ComboBoxStyle.DropDownList,
        };
        _cboSource.Items.AddRange(new object[] { "Sina", "EastMoney", "Tushare" });
        _cboSource.SelectedItem = settings.DataSource switch
        {
            "Tushare" => "Tushare",
            "EastMoney" => "EastMoney",
            _ => "Sina",
        };

        var lblToken = new Label { Text = "Tushare Token：", Left = 20, Top = 64, Width = 100 };
        _txtToken = new TextBox
        {
            Left = 20, Top = 88, Width = 400, Height = 28,
            Text = settings.TushareToken,
        };

        _chkDark = new CheckBox
        {
            Left = 20, Top = 130, Width = 200,
            Text = "深色模式 (Dark Mode)",
            Checked = settings.DarkMode,
        };
        _chkCache = new CheckBox
        {
            Left = 230, Top = 130, Width = 200,
            Text = "启用本地 SQLite 缓存",
            Checked = settings.EnableCache,
        };

        var lblHint = new Label
        {
            Left = 20, Top = 168, Width = 420, Height = 60,
            ForeColor = Color.Gray,
            Text = "提示：Sina / EastMoney 均免费、无需 Token。\n" +
                   "Tushare 需在 tushare.pro 注册账号获取 Token；搜索/实时报价始终走新浪。\n" +
                   "缓存目录：%AppData%\\StockTechAnalyzer\\cache.db",
        };

        var btnOk = new Button { Text = "确定", Left = 260, Top = 240, Width = 80, DialogResult = DialogResult.OK };
        var btnCancel = new Button { Text = "取消", Left = 350, Top = 240, Width = 80, DialogResult = DialogResult.Cancel };

        Controls.AddRange(new Control[] { lblSrc, _cboSource, lblToken, _txtToken,
            _chkDark, _chkCache, lblHint, btnOk, btnCancel });
        AcceptButton = btnOk;
        CancelButton = btnCancel;
    }
}
