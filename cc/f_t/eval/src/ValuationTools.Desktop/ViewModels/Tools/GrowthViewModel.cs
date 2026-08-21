using ValuationTools.Core.Calculators;

namespace ValuationTools.Desktop.ViewModels.Tools;

public sealed class GrowthViewModel : ToolViewModel
{
    public GrowthViewModel()
        : base("增长率测算",
               "折现率与增长率",
               "历史 CAGR、可持续增长率、基本面增长率与市场隐含增长率，为 DCF / PEG 提供有依据的增长假设。")
    {
        Formula = "CAGR = (期末/期初)^(1/n) − 1；可持续增长率 g = ROE × (1 − 分红率)";

        AddGroup("历史增长",
            Number("begin", "期初值（收入 / 利润）", 5000, " 万元"),
            Number("end", "期末值", 12000, " 万元"),
            Number("years", "间隔年数", 5, " 年"));

        AddGroup("内生增长能力",
            Percent("roe", "净资产收益率 ROE", 18),
            Percent("payout", "分红率", 30),
            Percent("reinvest", "再投资率", 60, "留存并投入扩张的比例"),
            Percent("roic", "投入资本回报率 ROIC", 15));

        AddGroup("市场隐含增长",
            Number("pe", "当前市盈率 PE", 22, "x"),
            Percent("discount", "股权成本 / 要求回报率", 9));

        Ready();
    }

    protected override void Compute()
    {
        var result = GrowthCalculator.Calculate(new GrowthInput
        {
            BeginningValue = V("begin"),
            EndingValue = V("end"),
            Years = V("years"),
            ReturnOnEquity = R("roe"),
            PayoutRatio = R("payout"),
            ReinvestmentRate = R("reinvest"),
            ReturnOnInvestedCapital = R("roic"),
            PeRatio = V("pe"),
            DiscountRate = R("discount")
        });

        AddResult("历史复合增长率 CAGR", Pct(result.Cagr), isPrimary: true);
        AddResult("累计增长倍数", Times(result.TotalGrowthMultiple));
        AddResult("可持续增长率 g = ROE × 留存率", Pct(result.SustainableGrowthRate), note: "不依赖外部融资可实现的增速上限");
        AddResult("基本面增长率 g = 再投资率 × ROIC", Pct(result.FundamentalGrowthRate));
        AddResult("当前 PE 隐含永续增长率", Pct(result.PeImpliedGrowthRate), note: "市场当前定价所隐含的长期增速");
        AddResult("按 CAGR 翻倍所需年数", result.YearsToDouble.HasValue ? Num(result.YearsToDouble.Value, 2) + " 年" : "—");
        AddResult("72 法则估算翻倍年数", result.RuleOf72Years.HasValue ? Num(result.RuleOf72Years.Value, 2) + " 年" : "—");

        SetNotice(result.Warning);
    }
}
