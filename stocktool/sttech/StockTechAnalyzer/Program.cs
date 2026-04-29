using System.Text;

namespace StockTechAnalyzer;

internal static class Program
{
    [STAThread]
    static void Main()
    {
        // 启用 GBK 解码（新浪/腾讯接口部分返回 GBK）
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

        // 让 ScottPlot 默认字体支持中文，避免标题/图例/标注显示成方块
        ScottPlot.Fonts.Default = PickChineseFont();

        ApplicationConfiguration.Initialize();
        Application.Run(new UI.MainForm());
    }

    private static string PickChineseFont()
    {
        // 按优先级尝试系统已安装的中文字体
        string[] candidates =
        {
            "Microsoft YaHei UI", "Microsoft YaHei",
            "SimHei", "SimSun", "NSimSun",
            "Noto Sans CJK SC", "Source Han Sans SC",
        };
        var installed = new HashSet<string>(
            new System.Drawing.Text.InstalledFontCollection().Families.Select(f => f.Name),
            StringComparer.OrdinalIgnoreCase);
        foreach (var name in candidates)
            if (installed.Contains(name)) return name;
        // 退而求其次：让 ScottPlot 根据中文字符自动探测
        return ScottPlot.Fonts.Detect("中");
    }
}
