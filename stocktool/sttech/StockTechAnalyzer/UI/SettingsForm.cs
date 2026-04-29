using System.ComponentModel;
using StockTechAnalyzer.Storage;

namespace StockTechAnalyzer.UI;

/// <summary>设置对话框：选择数据源 + 填写 Tushare Token。</summary>
internal sealed class SettingsForm : Form
{
    private readonly ComboBox _cboSource;
    private readonly TextBox _txtToken;

    public string DataSource => (string)_cboSource.SelectedItem!;
    public string TushareToken => _txtToken.Text.Trim();

    public SettingsForm(AppSettings settings)
    {
        Text = "设置";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false; MinimizeBox = false;
        ClientSize = new Size(440, 200);

        var lblSrc = new Label { Text = "数据源：", Left = 20, Top = 24, Width = 80 };
        _cboSource = new ComboBox
        {
            Left = 100, Top = 20, Width = 300,
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
            Left = 20, Top = 88, Width = 380, Height = 28,
            Text = settings.TushareToken,
            UseSystemPasswordChar = false,
        };

        var lblHint = new Label
        {
            Left = 20, Top = 122, Width = 400, Height = 30,
            ForeColor = Color.Gray,
            Text = "提示：Sina / EastMoney 均免费、无需 Token。\n" +
                   "Tushare 需在 tushare.pro 注册账号获取 Token；搜索/实时报价始终走新浪。",
        };

        var btnOk = new Button { Text = "确定", Left = 240, Top = 160, Width = 80, DialogResult = DialogResult.OK };
        var btnCancel = new Button { Text = "取消", Left = 330, Top = 160, Width = 80, DialogResult = DialogResult.Cancel };

        Controls.AddRange(new Control[] { lblSrc, _cboSource, lblToken, _txtToken, lblHint, btnOk, btnCancel });
        AcceptButton = btnOk;
        CancelButton = btnCancel;
    }
}
