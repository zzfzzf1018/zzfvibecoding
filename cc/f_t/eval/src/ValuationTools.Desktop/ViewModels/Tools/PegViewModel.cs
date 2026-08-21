using System.Data;
using ValuationTools.Core.Calculators;

namespace ValuationTools.Desktop.ViewModels.Tools;

public sealed class PegViewModel : ToolViewModel
{
    public PegViewModel()
        : base("PEG 市盈率相对增长",
               "相对估值",
               "彼得·林奇的 PEG 指标：把市盈率与盈利增长率放在一起看，PEG ≈ 1 通常被视为合理。")
    {
        Formula = "PEG = PE ÷ 盈利增长率(%)；合理 PE = 增长率(%) × 目标 PEG";

        AddGroup("估值与盈利",
            Number("price", "当前股价", 30, " 元"),
            Number("eps", "每股收益 EPS", 1.5, " 元", "可用 TTM 或明年预测值"),
            Number("pe", "直接给定市盈率", 0, "x", "填 0 则按 股价 ÷ EPS 计算"));

        AddGroup("增长与基准",
            Percent("growth", "预期盈利增长率", 20, "未来 3~5 年的年复合增速"),
            Percent("dividend", "股息率", 1.5, "用于计算 PEGY"),
            Number("targetPeg", "合理 PEG 基准", 1.0, null, "成长股常用 1，高确定性龙头可放宽到 1.2~1.5"),
            Number("holdYears", "持有年限", 3, " 年"));

        Ready();
    }

    protected override void Compute()
    {
        var result = PegCalculator.Calculate(new PegInput
        {
            Price = V("price"),
            EarningsPerShare = V("eps"),
            GivenPeRatio = V("pe"),
            EarningsGrowthRate = R("growth"),
            DividendYield = R("dividend"),
            TargetPeg = V("targetPeg"),
            HoldingYears = I("holdYears")
        });

        AddResult("PEG", Num(result.Peg, 2), isPrimary: true, note: result.Judgement);
        AddResult("当前市盈率 PE", Times(result.PeRatio));
        AddResult("PEGY（含股息）", Num(result.Pegy, 2));
        AddResult("合理市盈率", Times(result.FairPeRatio));
        AddResult("合理股价", Money(result.FairPrice) + " 元");
        AddResult("相对当前股价", Pct(result.UpsidePercent));
        AddResult("当前股价隐含增长率", Pct(result.ImpliedGrowthRate), note: "市场已price in 的增速");
        AddResult($"{I("holdYears")} 年后每股收益", Money(result.ForwardEps) + " 元");
        AddResult($"{I("holdYears")} 年目标价", Money(result.TargetPrice) + " 元");
        AddResult("预期年化回报（含股息）", Pct(result.ExpectedAnnualReturn));

        SetSensitivity(BuildSensitivity());
        SetNotice(result.Warning);
    }

    private DataTable BuildSensitivity()
    {
        double baseGrowth = R("growth");
        double[] growthOffsets = { -0.10, -0.05, 0, 0.05, 0.10 };
        double[] pegLevels = { 0.75, 1.0, 1.25, 1.5, 2.0 };

        var headers = new List<string> { "增长率 \\ 目标 PEG" };
        headers.AddRange(pegLevels.Select(peg => Num(peg, 2)));
        var table = CreateTable(headers.ToArray());

        foreach (var offset in growthOffsets)
        {
            double growth = baseGrowth + offset;
            var row = table.NewRow();
            row[0] = Pct(growth);
            for (int i = 0; i < pegLevels.Length; i++)
            {
                try
                {
                    var value = PegCalculator.Calculate(new PegInput
                    {
                        Price = V("price"),
                        EarningsPerShare = V("eps"),
                        GivenPeRatio = V("pe"),
                        EarningsGrowthRate = growth,
                        DividendYield = R("dividend"),
                        TargetPeg = pegLevels[i],
                        HoldingYears = I("holdYears")
                    }).FairPrice;
                    row[i + 1] = Money(value);
                }
                catch
                {
                    row[i + 1] = "—";
                }
            }
            table.Rows.Add(row);
        }

        SensitivityTitle = "合理股价敏感性（元）";
        return table;
    }
}
