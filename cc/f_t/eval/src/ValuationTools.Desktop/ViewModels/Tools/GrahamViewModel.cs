using ValuationTools.Core.Calculators;

namespace ValuationTools.Desktop.ViewModels.Tools;

public sealed class GrahamViewModel : ToolViewModel
{
    public GrahamViewModel()
        : base("格雷厄姆内在价值",
               "相对估值",
               "本杰明·格雷厄姆的经典内在价值公式、格雷厄姆数与净流动资产价值（NCAV）三重安全边际检验。")
    {
        Formula = "V = EPS × (8.5 + 2g) × 4.4 ÷ Y；格雷厄姆数 = √(22.5 × EPS × BVPS)";

        AddGroup("每股数据",
            Number("eps", "每股收益 EPS", 1.5, " 元"),
            Number("bvps", "每股净资产 BVPS", 8, " 元"),
            Percent("growth", "预期 7~10 年增长率", 8),
            Number("price", "当前股价", 20, " 元"));

        AddGroup("市场利率与安全边际",
            Percent("bondYield", "AAA 级公司债收益率 Y", 4.4, "修正公式的利率基准"),
            Percent("mos", "要求的安全边际", 30));

        AddGroup("清算价值检验（可选）",
            Number("currentAssets", "流动资产总额", 60000, " 万元"),
            Number("liabilities", "负债总额", 30000, " 万元"),
            Number("shares", "总股本", 10000, " 万股"));

        Ready();
    }

    protected override void Compute()
    {
        var result = GrahamCalculator.Calculate(new GrahamInput
        {
            EarningsPerShare = V("eps"),
            BookValuePerShare = V("bvps"),
            GrowthRate = R("growth"),
            CorporateBondYield = R("bondYield"),
            CurrentPrice = V("price"),
            CurrentAssets = V("currentAssets"),
            TotalLiabilities = V("liabilities"),
            SharesOutstanding = V("shares"),
            MarginOfSafety = R("mos")
        });

        AddResult("修正公式内在价值", Money(result.RevisedValue) + " 元", isPrimary: true, note: result.Judgement);
        AddResult("原始公式内在价值", Money(result.ClassicValue) + " 元", note: "V = EPS × (8.5 + 2g)");
        AddResult("格雷厄姆数", Money(result.GrahamNumber) + " 元", note: "防御型投资者的价格上限");
        AddResult("建议买入价（含安全边际）", Money(result.BuyBelowPrice) + " 元");
        AddResult("相对当前股价", Pct(result.UpsidePercent));
        AddResult("每股净流动资产 NCAV", Money(result.NetCurrentAssetValuePerShare) + " 元");
        AddResult("NCAV 的 2/3（烟蒂股买入线）", Money(result.NcavBuyPrice) + " 元");

        SetNotice(result.Warning);
    }
}
