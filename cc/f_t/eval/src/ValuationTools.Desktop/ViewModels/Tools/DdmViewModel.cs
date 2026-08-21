using System.Data;
using ValuationTools.Core.Calculators;

namespace ValuationTools.Desktop.ViewModels.Tools;

public sealed class DdmViewModel : ToolViewModel
{
    public DdmViewModel()
        : base("DDM 股利折现",
               "绝对估值",
               "两阶段股利折现模型，适用于分红稳定、派息政策清晰的成熟公司（银行、公用事业、消费龙头）。")
    {
        Formula = "P0 = Σ Dt / (1+Re)^t + D(n+1) / [(Re − g) × (1+Re)^n]";

        AddGroup("股利假设",
            Number("d0", "最近一期每股股利 D0", 1.2, " 元"),
            Number("years", "高增长期年数", 5, " 年", "填 0 则退化为单阶段戈登模型"),
            Percent("g1", "高增长期股利增长率", 10),
            Percent("g2", "永续增长率", 3),
            Toggle("fade", "增长率线性衰减到永续水平", true, "更贴近现实的过渡假设"));

        AddGroup("折现率与市价",
            Percent("re", "股权成本 Re", 9, "可由 CAPM 求得：Rf + β × 市场风险溢价"),
            Number("price", "当前股价", 20, " 元"));

        Ready();
    }

    protected override void Compute()
    {
        var input = new DdmInput
        {
            CurrentDividend = V("d0"),
            HighGrowthYears = I("years"),
            HighGrowthRate = R("g1"),
            StableGrowthRate = R("g2"),
            CostOfEquity = R("re"),
            LinearFade = B("fade"),
            CurrentPrice = V("price")
        };

        var result = DdmCalculator.Calculate(input);

        AddResult("每股内在价值", Money(result.IntrinsicValue) + " 元", isPrimary: true);
        AddResult("相对当前股价", Pct(result.UpsidePercent), note: result.UpsidePercent is > 0 ? "低估" : result.UpsidePercent is null ? null : "高估");
        AddResult("高增长期股利现值", Money(result.PresentValueOfDividends) + " 元");
        AddResult("永续期终值", Money(result.TerminalValue) + " 元");
        AddResult("终值现值", Money(result.PresentValueOfTerminal) + " 元");
        AddResult("按内在价值的股息率", Pct(result.DividendYieldOnCost));
        AddResult("当前股价隐含预期回报率", Pct(result.ImpliedReturn), note: "买入并长期持有的年化回报");

        if (result.Years.Count > 0)
        {
            var table = CreateTable("年份", "增长率", "每股股利", "折现因子", "现值");
            foreach (var row in result.Years)
                table.Rows.Add($"第 {row.Year} 年", Pct(row.GrowthRate), Money(row.Dividend), Num(row.DiscountFactor, 4), Money(row.PresentValue));
            SetSchedule(table);
        }

        SetSensitivity(BuildSensitivity());
        SetNotice(result.Warning);
    }

    private DataTable BuildSensitivity()
    {
        double baseRe = R("re");
        double baseG = R("g2");
        double[] offsets = { -0.01, -0.005, 0, 0.005, 0.01 };

        var headers = new List<string> { "永续增长率 \\ 股权成本" };
        headers.AddRange(offsets.Select(offset => Pct(baseRe + offset)));
        var table = CreateTable(headers.ToArray());

        foreach (var gOffset in offsets)
        {
            var row = table.NewRow();
            double g = baseG + gOffset;
            row[0] = Pct(g);
            for (int i = 0; i < offsets.Length; i++)
            {
                try
                {
                    var value = DdmCalculator.Calculate(new DdmInput
                    {
                        CurrentDividend = V("d0"),
                        HighGrowthYears = I("years"),
                        HighGrowthRate = R("g1"),
                        StableGrowthRate = g,
                        CostOfEquity = baseRe + offsets[i],
                        LinearFade = B("fade")
                    }).IntrinsicValue;
                    row[i + 1] = Money(value);
                }
                catch
                {
                    row[i + 1] = "—";
                }
            }
            table.Rows.Add(row);
        }
        return table;
    }
}
