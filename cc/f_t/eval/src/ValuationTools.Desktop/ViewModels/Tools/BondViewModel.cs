using System.Data;
using ValuationTools.Core.Calculators;

namespace ValuationTools.Desktop.ViewModels.Tools;

public sealed class BondViewModel : ToolViewModel
{
    public BondViewModel()
        : base("债券估值",
               "现金流工具",
               "债券定价、到期收益率反推、麦考利久期、修正久期与凸性，用于利率风险测算。")
    {
        Formula = "P = Σ C/(1+y/m)^t + F/(1+y/m)^n；修正久期 ≈ 麦考利久期 ÷ (1 + y/m)";

        AddGroup("债券条款",
            Number("face", "面值", 100, " 元"),
            Percent("coupon", "票面利率", 4),
            Number("years", "剩余期限", 5, " 年"),
            Number("frequency", "每年付息次数", 1, " 次"));

        AddGroup("收益率与市价",
            Percent("ytm", "到期收益率 YTM", 3.5, "市场价格为 0 时使用该收益率定价"),
            Number("price", "市场价格", 0, " 元", "填入市价则反求到期收益率"));

        Ready();
    }

    protected override void Compute()
    {
        var result = BondCalculator.Calculate(new BondInput
        {
            FaceValue = V("face"),
            CouponRate = R("coupon"),
            YearsToMaturity = V("years"),
            YieldToMaturity = R("ytm"),
            PaymentsPerYear = I("frequency"),
            MarketPrice = V("price")
        });

        AddResult("理论价格", Money(result.Price) + " 元", isPrimary: true, note: result.Judgement);
        AddResult("反推到期收益率", result.ImpliedYieldToMaturity.HasValue ? Pct(result.ImpliedYieldToMaturity.Value) : "—", note: "填写市场价格后计算");
        AddResult("当期收益率", Pct(result.CurrentYield));
        AddResult("票息现值", Money(result.PresentValueOfCoupons) + " 元");
        AddResult("本金现值", Money(result.PresentValueOfPrincipal) + " 元");
        AddResult("麦考利久期", Num(result.MacaulayDuration, 3) + " 年");
        AddResult("修正久期", Num(result.ModifiedDuration, 3));
        AddResult("凸性", Num(result.Convexity, 3));
        AddResult("利率上行 1% 的价格变动", Pct(result.PriceChangeIfRateUp1Pct), note: "含凸性修正的估计值");

        SetSensitivity(BuildSensitivity());
    }

    private DataTable BuildSensitivity()
    {
        double baseYield = V("price") > 0
            ? BondCalculator.Calculate(new BondInput
            {
                FaceValue = V("face"),
                CouponRate = R("coupon"),
                YearsToMaturity = V("years"),
                YieldToMaturity = R("ytm"),
                PaymentsPerYear = I("frequency"),
                MarketPrice = V("price")
            }).ImpliedYieldToMaturity ?? R("ytm")
            : R("ytm");

        double[] yieldOffsets = { -0.02, -0.01, -0.005, 0, 0.005, 0.01, 0.02 };

        var table = CreateTable("到期收益率", "理论价格", "价格变动");

        double basePrice = PriceAt(baseYield);
        foreach (var offset in yieldOffsets)
        {
            double price = PriceAt(baseYield + offset);
            table.Rows.Add(Pct(baseYield + offset), Money(price), basePrice > 0 ? Pct(price / basePrice - 1) : "—");
        }

        SensitivityTitle = "利率变动对债券价格的影响";
        return table;

        double PriceAt(double y) => BondCalculator.Calculate(new BondInput
        {
            FaceValue = V("face"),
            CouponRate = R("coupon"),
            YearsToMaturity = V("years"),
            YieldToMaturity = y,
            PaymentsPerYear = I("frequency"),
            MarketPrice = 0
        }).Price;
    }
}
