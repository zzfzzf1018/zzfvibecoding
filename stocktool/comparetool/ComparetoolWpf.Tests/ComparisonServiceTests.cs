using ComparetoolWpf.Models;
using ComparetoolWpf.Services;
using Xunit;

namespace ComparetoolWpf.Tests;

public class ComparisonServiceTests
{
    [Fact]
    public void ComparePeriods_BasicPlusHighlight()
    {
        var b = new FinancialReport
        {
            Kind = ReportKind.Income,
            ReportDate = new DateTime(2023, 12, 31),
            Items = { ["营业总收入"] = 100, ["净利润"] = 10, ["仅基准期"] = 5 },
        };
        var c = new FinancialReport
        {
            Kind = ReportKind.Income,
            ReportDate = new DateTime(2024, 12, 31),
            Items = { ["营业总收入"] = 130, ["净利润"] = 11, ["仅对比期"] = 7 },
        };
        var rows = ComparisonService.ComparePeriods(b, c, 0.2);

        var rev = Assert.Single(rows, r => r.Item == "营业总收入");
        Assert.Equal(0.3, rev.ChangeRatio!.Value, 6);
        Assert.True(rev.IsHighlighted);                   // 30% >= 20%

        var np = Assert.Single(rows, r => r.Item == "净利润");
        Assert.False(np.IsHighlighted);                   // 10% < 20%

        Assert.Contains(rows, r => r.Item == "仅基准期" && r.CompareValue == null);
        Assert.Contains(rows, r => r.Item == "仅对比期" && r.BaseValue == null);
    }

    [Fact]
    public void ComparePeriods_MismatchKind_Throws()
    {
        var b = new FinancialReport { Kind = ReportKind.Balance };
        var c = new FinancialReport { Kind = ReportKind.Income };
        Assert.Throws<ArgumentException>(() => ComparisonService.ComparePeriods(b, c));
    }

    [Theory]
    [InlineData(ReportKind.Balance, "资产总计")]
    [InlineData(ReportKind.Income, "营业总收入")]
    [InlineData(ReportKind.CashFlow, "经营活动现金流入小计")]
    public void CommonSizeCompare_ChoosesBaseItem(ReportKind kind, string baseItem)
    {
        var r1 = new FinancialReport
        {
            Kind = kind,
            StockFullCode = "SH1",
            Items = { [baseItem] = 200, ["子项"] = 50 },
        };
        var r2 = new FinancialReport
        {
            Kind = kind,
            StockFullCode = "SH2",
            Items = { [baseItem] = 0, ["子项"] = 10 }, // 基准为 0 应得 null
        };
        var rows = ComparisonService.CommonSizeCompare(new[] { r1, r2 }, kind);
        var sub = Assert.Single(rows, r => r.Item == "子项");
        Assert.Equal(0.25, sub.Percentages["SH1"]);
        Assert.Null(sub.Percentages["SH2"]);
    }

    [Fact]
    public void CommonSizeCompare_EmptyInput_ReturnsEmpty()
    {
        var rows = ComparisonService.CommonSizeCompare(Array.Empty<FinancialReport>(), ReportKind.Income);
        Assert.Empty(rows);
    }

    [Fact]
    public void CommonSizeCompare_KindMismatch_Throws()
    {
        var r1 = new FinancialReport { Kind = ReportKind.Balance };
        Assert.Throws<ArgumentException>(() =>
            ComparisonService.CommonSizeCompare(new[] { r1 }, ReportKind.Income));
    }
}
