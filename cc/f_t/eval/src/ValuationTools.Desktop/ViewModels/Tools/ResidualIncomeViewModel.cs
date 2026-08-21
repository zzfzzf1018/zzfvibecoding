using System.Data;
using ValuationTools.Core.Calculators;

namespace ValuationTools.Desktop.ViewModels.Tools;

public sealed class ResidualIncomeViewModel : ToolViewModel
{
    public ResidualIncomeViewModel()
        : base("剩余收益模型 RIM",
               "绝对估值",
               "价值 = 账面净资产 + 未来超额收益现值。对不分红、现金流波动大的公司比 DCF 更稳健。")
    {
        Formula = "V0 = B0 + Σ (ROE − Re) × B(t−1) / (1+Re)^t + 终值现值";

        AddGroup("盈利能力假设",
            Number("bvps", "期初每股净资产 B0", 8, " 元"),
            Percent("roe", "预期净资产收益率 ROE", 18),
            Percent("payout", "分红率", 30, "留存部分推动净资产增长"),
            Number("years", "明确预测年数", 10, " 年"));

        AddGroup("折现率与终值",
            Percent("re", "股权成本 Re", 9),
            Number("persistence", "剩余收益持续性因子 ω", 0.6, null, "0 = 超额收益立即消失，1 = 永续保持，一般取 0.4~0.8"),
            Number("price", "当前股价", 20, " 元"));

        Ready();
    }

    protected override void Compute()
    {
        var result = ResidualIncomeCalculator.Calculate(new ResidualIncomeInput
        {
            BookValuePerShare = V("bvps"),
            ReturnOnEquity = R("roe"),
            CostOfEquity = R("re"),
            PayoutRatio = R("payout"),
            ForecastYears = I("years"),
            PersistenceFactor = V("persistence"),
            CurrentPrice = V("price")
        });

        AddResult("每股内在价值", Money(result.IntrinsicValue) + " 元", isPrimary: true);
        AddResult("相对当前股价", Pct(result.UpsidePercent));
        AddResult("账面净资产部分", Money(V("bvps")) + " 元");
        AddResult("超额收益现值", Money(result.PresentValueOfResidualIncome) + " 元");
        AddResult("终值现值", Money(result.PresentValueOfTerminal) + " 元");
        AddResult("超出账面的溢价", Money(result.PremiumOverBookValue) + " 元", note: "特许经营权 / 护城河价值");
        AddResult("隐含合理 PB", Times(result.ImpliedPb));

        var table = CreateTable("年份", "期初净资产", "每股收益", "股权资本成本", "剩余收益", "现值");
        foreach (var row in result.Years)
            table.Rows.Add($"第 {row.Year} 年", Money(row.BeginningBookValue), Money(row.Earnings),
                Money(row.EquityCharge), Money(row.ResidualIncome), Money(row.PresentValue));

        SetSchedule(table);
        SetSensitivity(BuildSensitivity());
        SetNotice(result.Warning);
    }

    private DataTable BuildSensitivity()
    {
        double baseRoe = R("roe");
        double baseRe = R("re");
        double[] roeOffsets = { -0.04, -0.02, 0, 0.02, 0.04 };
        double[] reOffsets = { -0.02, -0.01, 0, 0.01, 0.02 };

        var headers = new List<string> { "股权成本 \\ ROE" };
        headers.AddRange(roeOffsets.Select(offset => Pct(baseRoe + offset)));
        var table = CreateTable(headers.ToArray());

        foreach (var reOffset in reOffsets)
        {
            var row = table.NewRow();
            double re = baseRe + reOffset;
            row[0] = Pct(re);
            for (int i = 0; i < roeOffsets.Length; i++)
            {
                try
                {
                    var value = ResidualIncomeCalculator.Calculate(new ResidualIncomeInput
                    {
                        BookValuePerShare = V("bvps"),
                        ReturnOnEquity = baseRoe + roeOffsets[i],
                        CostOfEquity = re,
                        PayoutRatio = R("payout"),
                        ForecastYears = I("years"),
                        PersistenceFactor = V("persistence")
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

        SensitivityTitle = "每股价值敏感性（元）";
        return table;
    }
}
