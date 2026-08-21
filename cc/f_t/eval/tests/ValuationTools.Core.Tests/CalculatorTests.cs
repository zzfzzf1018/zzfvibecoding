using ValuationTools.Core.Calculators;
using ValuationTools.Core.Common;
using Xunit;

namespace ValuationTools.Core.Tests;

public class CalculatorTests
{
    [Fact]
    public void Npv_MatchesManualCalculation()
    {
        var flows = new[] { -1000.0, 500, 500, 500 };
        double npv = FinancialMath.Npv(0.1, flows);
        Assert.Equal(243.43, npv, 2);
    }

    [Fact]
    public void Irr_ReturnsRateWhereNpvIsZero()
    {
        var flows = new[] { -1000.0, 500, 500, 500 };
        double irr = FinancialMath.Irr(flows)!.Value;
        Assert.Equal(0, FinancialMath.Npv(irr, flows), 6);
    }

    [Fact]
    public void SingleYearDcf_WithGordonTerminal_MatchesClosedForm()
    {
        var result = DcfCalculator.Calculate(new DcfInput
        {
            BaseCashFlow = 100,
            Stage1Years = 1,
            Stage1Growth = 0,
            Stage2Years = 0,
            DiscountRate = 0.10,
            TerminalGrowth = 0.02,
            SharesOutstanding = 1,
            NetDebt = 0
        });

        // FCF1 = 100，现值 = 100/1.1；终值 = 102/0.08，现值 = 1275/1.1
        Assert.Equal(100 / 1.1 + (102 / 0.08) / 1.1, result.EnterpriseValue, 6);
    }

    [Fact]
    public void Dcf_NetDebtReducesEquityValue()
    {
        var input = new DcfInput
        {
            BaseCashFlow = 1000,
            Stage1Years = 5,
            Stage1Growth = 0.05,
            DiscountRate = 0.09,
            TerminalGrowth = 0.02,
            NetDebt = 2000,
            SharesOutstanding = 100
        };

        var result = DcfCalculator.Calculate(input);
        Assert.Equal(result.EnterpriseValue - 2000, result.EquityValue, 6);
        Assert.Equal(result.EquityValue / 100, result.ValuePerShare, 6);
    }

    [Fact]
    public void Dcf_ThrowsWhenTerminalGrowthExceedsDiscountRate()
    {
        Assert.Throws<InvalidOperationException>(() => DcfCalculator.Calculate(new DcfInput
        {
            BaseCashFlow = 100,
            Stage1Years = 3,
            DiscountRate = 0.05,
            TerminalGrowth = 0.06,
            SharesOutstanding = 1
        }));
    }

    [Fact]
    public void Ddm_SingleStage_EqualsGordonFormula()
    {
        var result = DdmCalculator.Calculate(new DdmInput
        {
            CurrentDividend = 2,
            HighGrowthYears = 0,
            StableGrowthRate = 0.03,
            CostOfEquity = 0.08
        });

        Assert.Equal(2 * 1.03 / 0.05, result.IntrinsicValue, 6);
    }

    [Fact]
    public void Ddm_ImpliedReturn_ReproducesMarketPrice()
    {
        var input = new DdmInput
        {
            CurrentDividend = 1.5,
            HighGrowthYears = 5,
            HighGrowthRate = 0.08,
            StableGrowthRate = 0.03,
            CostOfEquity = 0.09,
            CurrentPrice = 40
        };

        double implied = DdmCalculator.Calculate(input).ImpliedReturn!.Value;
        var check = DdmCalculator.Calculate(new DdmInput
        {
            CurrentDividend = 1.5,
            HighGrowthYears = 5,
            HighGrowthRate = 0.08,
            StableGrowthRate = 0.03,
            CostOfEquity = implied
        });

        Assert.Equal(40, check.IntrinsicValue, 4);
    }

    [Fact]
    public void Wacc_UsesMarketValueWeights()
    {
        var result = DiscountRateCalculator.Calculate(new DiscountRateInput
        {
            RiskFreeRate = 0.03,
            Beta = 1.2,
            MarketRiskPremium = 0.06,
            MarketValueOfEquity = 800,
            MarketValueOfDebt = 200,
            CostOfDebt = 0.05,
            TaxRate = 0.25
        });

        Assert.Equal(0.102, result.CostOfEquity, 6);
        Assert.Equal(0.0375, result.AfterTaxCostOfDebt, 6);
        Assert.Equal(0.102 * 0.8 + 0.0375 * 0.2, result.Wacc, 6);
    }

    [Fact]
    public void Peg_UsesGrowthInPercentagePoints()
    {
        var result = PegCalculator.Calculate(new PegInput
        {
            Price = 30,
            EarningsPerShare = 1.5,
            EarningsGrowthRate = 0.20,
            TargetPeg = 1.0
        });

        Assert.Equal(20, result.PeRatio, 6);
        Assert.Equal(1.0, result.Peg, 6);
        Assert.Equal(30, result.FairPrice, 6);
    }

    [Fact]
    public void RelativeValuation_ImpliedPriceFromPeerPe()
    {
        var result = RelativeValuationCalculator.Calculate(new RelativeValuationInput
        {
            Price = 30,
            SharesOutstanding = 1000,
            EarningsPerShare = 2,
            PeerPe = 20
        });

        var peRow = result.Rows.Single(r => r.Method.Contains("PE"));
        Assert.Equal(40, peRow.ImpliedPrice, 6);
        Assert.Equal(40, result.AverageImpliedPrice, 6);
    }

    [Fact]
    public void ResidualIncome_WhenRoeEqualsCostOfEquity_ValueEqualsBookValue()
    {
        var result = ResidualIncomeCalculator.Calculate(new ResidualIncomeInput
        {
            BookValuePerShare = 10,
            ReturnOnEquity = 0.09,
            CostOfEquity = 0.09,
            PayoutRatio = 0.3,
            ForecastYears = 10,
            PersistenceFactor = 0.6
        });

        Assert.Equal(10, result.IntrinsicValue, 6);
        Assert.Equal(1, result.ImpliedPb, 6);
    }

    [Fact]
    public void Graham_NumberUsesTwentyTwoPointFive()
    {
        var result = GrahamCalculator.Calculate(new GrahamInput
        {
            EarningsPerShare = 2,
            BookValuePerShare = 10,
            GrowthRate = 0.05,
            CorporateBondYield = 0.044
        });

        Assert.Equal(Math.Sqrt(22.5 * 2 * 10), result.GrahamNumber, 6);
        Assert.Equal(2 * (8.5 + 10), result.ClassicValue, 6);
        Assert.Equal(result.ClassicValue, result.RevisedValue, 6);
    }

    [Fact]
    public void Bond_PricedAtPar_WhenYieldEqualsCoupon()
    {
        var result = BondCalculator.Calculate(new BondInput
        {
            FaceValue = 100,
            CouponRate = 0.04,
            YearsToMaturity = 5,
            YieldToMaturity = 0.04,
            PaymentsPerYear = 1
        });

        Assert.Equal(100, result.Price, 6);
        Assert.True(result.MacaulayDuration is > 4 and < 5);
    }

    [Fact]
    public void Bond_SolvesYieldFromMarketPrice()
    {
        var result = BondCalculator.Calculate(new BondInput
        {
            FaceValue = 100,
            CouponRate = 0.05,
            YearsToMaturity = 10,
            PaymentsPerYear = 2,
            MarketPrice = 95
        });

        Assert.NotNull(result.ImpliedYieldToMaturity);
        Assert.True(result.ImpliedYieldToMaturity > 0.05);
    }

    [Fact]
    public void Option_SatisfiesPutCallParity()
    {
        var input = new OptionInput
        {
            SpotPrice = 100,
            StrikePrice = 95,
            TimeToMaturity = 1,
            RiskFreeRate = 0.03,
            Volatility = 0.25
        };

        var result = OptionCalculator.Calculate(input);
        double parity = result.CallPrice - result.PutPrice;
        double expected = 100 - 95 * Math.Exp(-0.03);
        Assert.Equal(expected, parity, 4);
    }

    [Fact]
    public void Project_NpvAndProfitabilityIndexAreConsistent()
    {
        var result = ProjectCashFlowCalculator.Calculate(new ProjectCashFlowInput
        {
            InitialInvestment = 1000,
            CashFlows = new[] { 400.0, 400, 400 },
            DiscountRate = 0.1
        });

        Assert.Equal(result.TotalPresentValue - 1000, result.Npv, 6);
        Assert.Equal(result.TotalPresentValue / 1000, result.ProfitabilityIndex, 6);
        Assert.NotNull(result.Irr);
    }

    [Fact]
    public void Growth_SustainableGrowthEqualsRoeTimesRetention()
    {
        var result = GrowthCalculator.Calculate(new GrowthInput
        {
            BeginningValue = 100,
            EndingValue = 200,
            Years = 5,
            ReturnOnEquity = 0.20,
            PayoutRatio = 0.4
        });

        Assert.Equal(0.12, result.SustainableGrowthRate, 6);
        Assert.Equal(Math.Pow(2, 0.2) - 1, result.Cagr!.Value, 6);
    }

    [Fact]
    public void Growth_PeImpliedGrowthIsUndefinedWithoutDividend()
    {
        var result = GrowthCalculator.Calculate(new GrowthInput
        {
            PeRatio = 20,
            DiscountRate = 0.09,
            PayoutRatio = 0
        });

        Assert.Null(result.PeImpliedGrowthRate);
    }

    [Fact]
    public void Dcf_RejectsExcessiveForecastHorizon()
    {
        Assert.Throws<ArgumentException>(() => DcfCalculator.Calculate(new DcfInput
        {
            BaseCashFlow = 100,
            Stage1Years = 200,
            DiscountRate = 0.10,
            TerminalGrowth = 0.02,
            SharesOutstanding = 1
        }));
    }

    [Fact]
    public void Dcf_WarnsWhenShareCountIsMissing()
    {
        var result = DcfCalculator.Calculate(new DcfInput
        {
            BaseCashFlow = 100,
            Stage1Years = 5,
            DiscountRate = 0.10,
            TerminalGrowth = 0.02,
            SharesOutstanding = 0
        });

        Assert.NotNull(result.Warning);
        Assert.Equal(0, result.ValuePerShare);
    }
}
