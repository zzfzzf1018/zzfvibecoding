using ComparetoolWpf.Models;
using ComparetoolWpf.Services;
using Xunit;

namespace ComparetoolWpf.Tests;

public class MetricsServiceTests
{
    [Fact]
    public void ComputeGrowth_YoYAndQoQ()
    {
        var reports = new[]
        {
            MakeIncome(new DateTime(2022, 12, 31), 100),
            MakeIncome(new DateTime(2023, 6, 30), 50),
            MakeIncome(new DateTime(2023, 12, 31), 130),
            MakeIncome(new DateTime(2024, 12, 31), 169),
        };
        var rows = MetricsService.ComputeGrowth(reports, "营业总收入");
        Assert.Equal(4, rows.Count);
        var newest = rows[0];                // 倒序
        Assert.Equal(new DateTime(2024, 12, 31), newest.ReportDate);
        Assert.Equal(0.3, newest.YoY!.Value, 6);             // 169/130-1
        Assert.Equal(169.0 / 130 - 1, newest.QoQ!.Value, 6); // 上一期是 130

        var earliest = rows.Last();
        Assert.Null(earliest.YoY);
        Assert.Null(earliest.QoQ);
    }

    [Fact]
    public void ComputeGrowth_BaseIsZero_ReturnsNull()
    {
        var reports = new[]
        {
            MakeIncome(new DateTime(2023, 12, 31), 0),
            MakeIncome(new DateTime(2024, 12, 31), 100),
        };
        var rows = MetricsService.ComputeGrowth(reports, "营业总收入");
        Assert.Null(rows[0].YoY);
    }

    [Fact]
    public void ComputeGrowth_MissingItem_ValueNull()
    {
        var reports = new[] { MakeIncome(new DateTime(2024, 12, 31), 100) };
        var rows = MetricsService.ComputeGrowth(reports, "不存在的指标");
        Assert.Null(rows[0].Value);
    }

    [Fact]
    public void ComputeDuPont_FullDecomposition()
    {
        var bal = TestData.Balance(new DateTime(2024, 12, 31), 100, 50, 1000, 500);
        var inc = TestData.Income(new DateTime(2024, 12, 31), 200, 20);
        var rows = MetricsService.ComputeDuPont(new[] { bal }, new[] { inc });
        var r = Assert.Single(rows);
        Assert.Equal(0.1, r.NetMargin!.Value, 6);
        Assert.Equal(0.2, r.AssetTurnover!.Value, 6);
        Assert.Equal(2.0, r.EquityMultiplier!.Value, 6);
        Assert.Equal(0.04, r.Roe!.Value, 6);
    }

    [Fact]
    public void ComputeDuPont_NoMatchingDate_Empty()
    {
        var bal = TestData.Balance(new DateTime(2024, 12, 31), 0, 0, 0, 0);
        var inc = TestData.Income(new DateTime(2023, 12, 31), 0, 0);
        Assert.Empty(MetricsService.ComputeDuPont(new[] { bal }, new[] { inc }));
    }

    [Fact]
    public void ComputeDuPont_FallbackRoe_WhenSomeFactorMissing()
    {
        // 资产为 0 → AssetTurnover null → ROE 退化为 NP/Equity
        var bal = TestData.Balance(new DateTime(2024, 12, 31), 0, 0, 0, 200);
        var inc = TestData.Income(new DateTime(2024, 12, 31), 1000, 50);
        var r = Assert.Single(MetricsService.ComputeDuPont(new[] { bal }, new[] { inc }));
        Assert.Null(r.AssetTurnover);
        Assert.Equal(0.25, r.Roe!.Value, 6);
    }

    [Fact]
    public void ComputeCashQuality_FullChain()
    {
        var bal1 = TestData.Balance(new DateTime(2023, 12, 31), 200, 100, 1000, 500, ar: 100, inv: 50, ap: 80);
        var bal2 = TestData.Balance(new DateTime(2024, 12, 31), 250, 120, 1100, 550, ar: 120, inv: 60, ap: 90);
        var inc1 = TestData.Income(new DateTime(2023, 12, 31), 365, 30, cost: 365);
        var inc2 = TestData.Income(new DateTime(2024, 12, 31), 730, 40, cost: 730);
        var cf1 = TestData.CashFlow(new DateTime(2023, 12, 31), 50, 30);
        var cf2 = TestData.CashFlow(new DateTime(2024, 12, 31), 70, 25);

        var rows = MetricsService.ComputeCashQuality(new[] { bal1, bal2 }, new[] { inc1, inc2 }, new[] { cf1, cf2 });
        Assert.Equal(2, rows.Count);
        var newest = rows[0];                // 倒序
        Assert.Equal(new DateTime(2024, 12, 31), newest.ReportDate);
        Assert.Equal(130, newest.WorkingCapital);          // 250-120
        Assert.Equal(30, newest.WorkingCapitalChange);     // 130 - 100
        Assert.Equal(45, newest.FreeCashFlow);             // 70 - |25|
        Assert.Equal(120.0 / 730 * 365, newest.DSO!.Value, 6);
        Assert.Equal(60.0 / 730 * 365, newest.DIO!.Value, 6);
        Assert.Equal(90.0 / 730 * 365, newest.DPO!.Value, 6);
        Assert.Equal(newest.DSO + newest.DIO - newest.DPO, newest.CCC);

        // 第一期没有上期，WCChange 应为 null
        Assert.Null(rows[1].WorkingCapitalChange);
    }

    [Fact]
    public void ComputeCashQuality_BalanceOnly_NoIncomeNoCashFlow()
    {
        var bal = TestData.Balance(new DateTime(2024, 12, 31), 100, 50, 0, 0);
        var rows = MetricsService.ComputeCashQuality(new[] { bal }, Array.Empty<FinancialReport>(), Array.Empty<FinancialReport>());
        var r = Assert.Single(rows);
        Assert.Equal(50, r.WorkingCapital);
        Assert.Null(r.FreeCashFlow);
        Assert.Null(r.DSO);
    }

    private static FinancialReport MakeIncome(DateTime date, double rev) =>
        new()
        {
            ReportDate = date,
            PeriodLabel = date.ToString("yyyy"),
            Kind = ReportKind.Income,
            Items = { ["营业总收入"] = rev },
        };
}
