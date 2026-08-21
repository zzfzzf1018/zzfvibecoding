namespace ValuationTools.Core.Calculators;

public sealed class DiscountRateInput
{
    public double RiskFreeRate { get; init; }
    public double Beta { get; init; }
    /// <summary>市场风险溢价（股票市场预期收益 - 无风险利率）。</summary>
    public double MarketRiskPremium { get; init; }
    /// <summary>规模溢价 / 特定风险溢价。</summary>
    public double SizePremium { get; init; }
    public double CountryRiskPremium { get; init; }

    public double MarketValueOfEquity { get; init; }
    public double MarketValueOfDebt { get; init; }
    /// <summary>税前债务成本。</summary>
    public double CostOfDebt { get; init; }
    public double TaxRate { get; init; }
}

public sealed class DiscountRateResult
{
    public double CostOfEquity { get; init; }
    public double AfterTaxCostOfDebt { get; init; }
    public double EquityWeight { get; init; }
    public double DebtWeight { get; init; }
    public double Wacc { get; init; }
    public double TotalCapital { get; init; }
    /// <summary>去杠杆 Beta（Hamada 公式）。</summary>
    public double UnleveredBeta { get; init; }
    public string? Warning { get; init; }
}

/// <summary>折现率工具：CAPM 求股权成本，并加权得到 WACC。</summary>
public static class DiscountRateCalculator
{
    public static DiscountRateResult Calculate(DiscountRateInput input)
    {
        double costOfEquity = input.RiskFreeRate
                              + input.Beta * input.MarketRiskPremium
                              + input.SizePremium
                              + input.CountryRiskPremium;

        double afterTaxCostOfDebt = input.CostOfDebt * (1 - input.TaxRate);
        double total = input.MarketValueOfEquity + input.MarketValueOfDebt;

        double equityWeight = total > 0 ? input.MarketValueOfEquity / total : 1.0;
        double debtWeight = total > 0 ? input.MarketValueOfDebt / total : 0.0;
        double wacc = costOfEquity * equityWeight + afterTaxCostOfDebt * debtWeight;

        double debtToEquity = input.MarketValueOfEquity > 0 ? input.MarketValueOfDebt / input.MarketValueOfEquity : 0;
        double unleveredBeta = input.Beta / (1 + (1 - input.TaxRate) * debtToEquity);

        string? warning = null;
        if (total <= 0)
            warning = "未填写股权与债务市值，WACC 已按 100% 股权计算。";
        else if (input.CostOfDebt > costOfEquity)
            warning = "债务成本高于股权成本，请检查输入是否合理。";

        return new DiscountRateResult
        {
            CostOfEquity = costOfEquity,
            AfterTaxCostOfDebt = afterTaxCostOfDebt,
            EquityWeight = equityWeight,
            DebtWeight = debtWeight,
            Wacc = wacc,
            TotalCapital = total,
            UnleveredBeta = unleveredBeta,
            Warning = warning
        };
    }
}
