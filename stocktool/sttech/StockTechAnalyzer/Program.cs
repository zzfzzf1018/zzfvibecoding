using System.Text;

namespace StockTechAnalyzer;

internal static class Program
{
    [STAThread]
    static void Main()
    {
        // 启用 GBK 解码（新浪/腾讯接口部分返回 GBK）
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

        ApplicationConfiguration.Initialize();
        Application.Run(new UI.MainForm());
    }
}
