using ComparetoolWpf.Models;
using Xunit;

namespace ComparetoolWpf.Tests;

public class ModelsTests
{
    [Fact]
    public void StockInfo_FullCode_SecuCode_ToString()
    {
        var s = new StockInfo("600000", "浦发银行", "SH");
        Assert.Equal("SH600000", s.FullCode);
        Assert.Equal("600000.SH", s.SecuCode);
        Assert.Equal("600000 浦发银行", s.ToString());
    }

    [Fact]
    public void ComparisonRow_AbsoluteChange_And_Ratio()
    {
        var r = new ComparisonRow { Item = "x", BaseValue = 100, CompareValue = 120 };
        Assert.Equal(20, r.AbsoluteChange);
        Assert.Equal(0.2, r.ChangeRatio);
    }

    [Fact]
    public void ComparisonRow_NullBase_ReturnsNull()
    {
        var r = new ComparisonRow { CompareValue = 1 };
        Assert.Null(r.AbsoluteChange);
        Assert.Null(r.ChangeRatio);
    }

    [Fact]
    public void ComparisonRow_ZeroBase_ReturnsNullRatio()
    {
        var r = new ComparisonRow { BaseValue = 0, CompareValue = 5 };
        Assert.Equal(5, r.AbsoluteChange);
        Assert.Null(r.ChangeRatio);
    }

    [Fact]
    public void MultiStockRow_Defaults()
    {
        var r = new MultiStockRow();
        Assert.Empty(r.RawValues);
        Assert.Empty(r.Percentages);
    }

    [Fact]
    public void FinancialReport_Defaults()
    {
        var f = new FinancialReport { ReportDate = new DateTime(2024, 12, 31) };
        Assert.Equal(ReportKind.Balance, f.Kind);
        Assert.NotNull(f.Items);
    }

    [Fact]
    public void EnumValues_AreStable()
    {
        Assert.Equal(0, (int)ReportKind.Balance);
        Assert.Equal(1, (int)ReportKind.Income);
        Assert.Equal(2, (int)ReportKind.CashFlow);
        Assert.Contains(ReportPeriodType.Annual, Enum.GetValues<ReportPeriodType>());
    }

    [Fact]
    public void GrowthRow_DuPontRow_CashQualityRow_Default()
    {
        var g = new GrowthRow { ReportDate = DateTime.Today, Value = 1 };
        Assert.Equal(1, g.Value);
        var d = new DuPontRow { ReportDate = DateTime.Today };
        Assert.Null(d.Roe);
        var c = new CashQualityRow { ReportDate = DateTime.Today };
        Assert.Null(c.CCC);
    }

    [Fact]
    public void ScreenerFilter_Defaults_PageSize200()
    {
        var f = new ScreenerFilter();
        Assert.Equal(200, f.PageSize);
        Assert.False(f.IncludeValuation);
    }

    [Fact]
    public void ScreenerRow_Defaults()
    {
        var r = new ScreenerRow();
        Assert.Equal(string.Empty, r.Code);
    }
}
