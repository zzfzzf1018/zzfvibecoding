using ValuationTools.Core.Common;

namespace ValuationTools.Core.Calculators;

public sealed class ResidualIncomeInput
{
    /// <summary>期初每股净资产 B0。</summary>
    public double BookValuePerShare { get; init; }
    public double ReturnOnEquity { get; init; }
    public double CostOfEquity { get; init; }
    /// <summary>分红率（派息 / 净利润）。</summary>
    public double PayoutRatio { get; init; }
    public int ForecastYears { get; init; } = 10;
    /// <summary>剩余收益的持续性因子 ω（0 = 竞争优势立即消失，1 = 永续保持）。</summary>
    public double PersistenceFactor { get; init; } = 0.6;
    public double CurrentPrice { get; init; }
}

public sealed record ResidualIncomeYearRow(
    int Year,
    double BeginningBookValue,
    double Earnings,
    double EquityCharge,
    double ResidualIncome,
    double PresentValue);

public sealed class ResidualIncomeResult
{
    public IReadOnlyList<ResidualIncomeYearRow> Years { get; init; } = Array.Empty<ResidualIncomeYearRow>();
    public double PresentValueOfResidualIncome { get; init; }
    public double TerminalValue { get; init; }
    public double PresentValueOfTerminal { get; init; }
    public double IntrinsicValue { get; init; }
    /// <summary>估值中超出账面净资产的部分（特许经营权价值）。</summary>
    public double PremiumOverBookValue { get; init; }
    public double ImpliedPb { get; init; }
    public double? UpsidePercent { get; init; }
    public string? Warning { get; init; }
}

/// <summary>剩余收益模型（RIM / EVA 思路）：价值 = 账面净资产 + 未来超额收益现值。</summary>
public static class ResidualIncomeCalculator
{
    public static ResidualIncomeResult Calculate(ResidualIncomeInput input)
    {
        if (input.BookValuePerShare <= 0)
            throw new ArgumentException("每股净资产必须大于 0。");
        if (input.ForecastYears <= 0 || input.ForecastYears > 100)
            throw new ArgumentException("预测年数应在 1~100 年之间。");
        double persistence = Math.Clamp(input.PersistenceFactor, 0, 0.999);

        var rows = new List<ResidualIncomeYearRow>();
        double book = input.BookValuePerShare;
        double pvResidual = 0;
        double lastResidual = 0;

        for (int year = 1; year <= input.ForecastYears; year++)
        {
            double earnings = book * input.ReturnOnEquity;
            double equityCharge = book * input.CostOfEquity;
            double residual = earnings - equityCharge;
            double pv = residual * FinancialMath.DiscountFactor(input.CostOfEquity, year);
            pvResidual += pv;
            rows.Add(new ResidualIncomeYearRow(year, book, earnings, equityCharge, residual, pv));

            book += earnings * (1 - input.PayoutRatio);
            lastResidual = residual;
        }

        // 持续性衰减终值：TV = RI_{n} * ω / (1 + r - ω)
        double terminalValue = lastResidual * persistence / (1 + input.CostOfEquity - persistence);
        double pvTerminal = terminalValue * FinancialMath.DiscountFactor(input.CostOfEquity, input.ForecastYears);
        double value = input.BookValuePerShare + pvResidual + pvTerminal;

        string? warning = null;
        if (input.ReturnOnEquity <= input.CostOfEquity)
            warning = "ROE 不高于股权成本，公司在毁灭价值，理论价值应低于账面净资产。";
        else if (input.ReturnOnEquity > 0.35)
            warning = "ROE 高于 35% 且长期维持的假设过于乐观，建议降低持续性因子。";

        return new ResidualIncomeResult
        {
            Years = rows,
            PresentValueOfResidualIncome = pvResidual,
            TerminalValue = terminalValue,
            PresentValueOfTerminal = pvTerminal,
            IntrinsicValue = value,
            PremiumOverBookValue = value - input.BookValuePerShare,
            ImpliedPb = value / input.BookValuePerShare,
            UpsidePercent = input.CurrentPrice > 0 ? value / input.CurrentPrice - 1.0 : null,
            Warning = warning
        };
    }
}
