using System.Data;
using ValuationTools.Core.Calculators;

namespace ValuationTools.Desktop.ViewModels.Tools;

public sealed class DiscountRateViewModel : ToolViewModel
{
    public DiscountRateViewModel()
        : base("折现率 WACC / CAPM",
               "折现率与增长率",
               "用 CAPM 计算股权成本，再按市值权重加权得到 WACC，作为 DCF 模型的折现率输入。")
    {
        Formula = "Re = Rf + β × MRP + 规模溢价 + 国家风险溢价；WACC = Re × E/(D+E) + Rd × (1−T) × D/(D+E)";

        AddGroup("股权成本（CAPM）",
            Percent("rf", "无风险利率 Rf", 2.5, "通常取 10 年期国债收益率"),
            Number("beta", "贝塔系数 β", 1.1, null, "个股相对市场的波动敏感度"),
            Percent("mrp", "市场风险溢价 MRP", 6, "A 股常用 5%~7%"),
            Percent("size", "规模 / 特定风险溢价", 1),
            Percent("crp", "国家风险溢价", 0));

        AddGroup("资本结构与债务成本",
            Number("equity", "股权市值 E", 80000, " 万元"),
            Number("debt", "债务市值 D", 20000, " 万元"),
            Percent("rd", "税前债务成本 Rd", 5),
            Percent("tax", "所得税率", 25));

        Ready();
    }

    protected override void Compute()
    {
        var result = DiscountRateCalculator.Calculate(new DiscountRateInput
        {
            RiskFreeRate = R("rf"),
            Beta = V("beta"),
            MarketRiskPremium = R("mrp"),
            SizePremium = R("size"),
            CountryRiskPremium = R("crp"),
            MarketValueOfEquity = V("equity"),
            MarketValueOfDebt = V("debt"),
            CostOfDebt = R("rd"),
            TaxRate = R("tax")
        });

        AddResult("WACC 加权平均资本成本", Pct(result.Wacc), isPrimary: true);
        AddResult("股权成本 Re", Pct(result.CostOfEquity));
        AddResult("税后债务成本", Pct(result.AfterTaxCostOfDebt));
        AddResult("股权权重 E/(D+E)", Pct(result.EquityWeight));
        AddResult("债务权重 D/(D+E)", Pct(result.DebtWeight));
        AddResult("总资本", Money0(result.TotalCapital) + " 万元");
        AddResult("去杠杆 Beta", Num(result.UnleveredBeta, 3), note: "Hamada 公式，用于可比公司 Beta 调整");

        SetSensitivity(BuildSensitivity());
        SetNotice(result.Warning);
    }

    private DataTable BuildSensitivity()
    {
        double baseBeta = V("beta");
        double baseMrp = R("mrp");
        double[] betaOffsets = { -0.3, -0.15, 0, 0.15, 0.3 };
        double[] mrpOffsets = { -0.02, -0.01, 0, 0.01, 0.02 };

        var headers = new List<string> { "市场风险溢价 \\ Beta" };
        headers.AddRange(betaOffsets.Select(offset => Num(baseBeta + offset, 2)));
        var table = CreateTable(headers.ToArray());

        foreach (var mrpOffset in mrpOffsets)
        {
            var row = table.NewRow();
            double mrp = baseMrp + mrpOffset;
            row[0] = Pct(mrp);
            for (int i = 0; i < betaOffsets.Length; i++)
            {
                var wacc = DiscountRateCalculator.Calculate(new DiscountRateInput
                {
                    RiskFreeRate = R("rf"),
                    Beta = baseBeta + betaOffsets[i],
                    MarketRiskPremium = mrp,
                    SizePremium = R("size"),
                    CountryRiskPremium = R("crp"),
                    MarketValueOfEquity = V("equity"),
                    MarketValueOfDebt = V("debt"),
                    CostOfDebt = R("rd"),
                    TaxRate = R("tax")
                }).Wacc;
                row[i + 1] = Pct(wacc);
            }
            table.Rows.Add(row);
        }

        SensitivityTitle = "WACC 敏感性分析";
        return table;
    }
}
