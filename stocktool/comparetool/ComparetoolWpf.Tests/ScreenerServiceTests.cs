using System.Net;
using System.Reflection;
using ComparetoolWpf.Models;
using ComparetoolWpf.Services;
using Newtonsoft.Json.Linq;
using Xunit;

namespace ComparetoolWpf.Tests;

public class ScreenerServiceTests
{
    [Theory]
    [InlineData(2026, 10, 7, 2026, 9, 30)]
    [InlineData(2026, 4, 15, 2026, 3, 31)]
    [InlineData(2026, 1, 1, 2025, 12, 31)]
    [InlineData(2026, 12, 31, 2026, 12, 31)]
    public void SnapToQuarterEnd_PicksLatestPast(int y, int m, int d, int ey, int em, int ed)
    {
        Assert.Equal(new DateTime(ey, em, ed),
            ScreenerService.SnapToQuarterEnd(new DateTime(y, m, d)));
    }

    [Theory]
    [InlineData(2024, 11, 15, 2024, 9, 30)]
    [InlineData(2024, 9, 5,   2024, 6, 30)]
    [InlineData(2024, 5, 2,   2024, 3, 31)]
    [InlineData(2024, 2, 1,   2023, 9, 30)]
    public void RecommendReportDate_AllBranches(int y, int m, int d, int ey, int em, int ed)
    {
        Assert.Equal(new DateTime(ey, em, ed),
            ScreenerService.RecommendReportDate(new DateTime(y, m, d)));
    }

    // ===== Pass / PassValuation 通过反射触达 =====

    private static bool Pass(ScreenerRow r, ScreenerFilter f) =>
        (bool)typeof(ScreenerService).GetMethod("Pass", BindingFlags.NonPublic | BindingFlags.Static)!
            .Invoke(null, new object[] { r, f })!;

    private static bool PassValuation(ScreenerRow r, ScreenerFilter f) =>
        (bool)typeof(ScreenerService).GetMethod("PassValuation", BindingFlags.NonPublic | BindingFlags.Static)!
            .Invoke(null, new object[] { r, f })!;

    [Fact]
    public void Pass_IndustryContains()
    {
        var r = new ScreenerRow { Industry = "白酒" };
        Assert.True(Pass(r, new ScreenerFilter { IndustryContains = "酒" }));
        Assert.False(Pass(r, new ScreenerFilter { IndustryContains = "银行" }));
        Assert.False(Pass(new ScreenerRow { Industry = "" }, new ScreenerFilter { IndustryContains = "酒" }));
    }

    [Fact]
    public void Pass_IndustryAnyOf()
    {
        var f = new ScreenerFilter { IndustryAnyOf = new() { "银行", "酒" } };
        Assert.True(Pass(new ScreenerRow { Industry = "白酒" }, f));
        Assert.False(Pass(new ScreenerRow { Industry = "钢铁" }, f));
        Assert.False(Pass(new ScreenerRow { Industry = "" }, f));
    }

    [Fact]
    public void Pass_NumericThresholds_MissingFails()
    {
        var f = new ScreenerFilter
        {
            RoeMin = 0.1,
            GrossMarginMin = 0.2,
            NetMarginMin = 0.05,
            RevenueYoYMin = 0,
            NetProfitYoYMin = 0,
            EpsMin = 0.1,
        };
        Assert.False(Pass(new ScreenerRow(), f));
        var ok = new ScreenerRow
        {
            Roe = 0.2, GrossMargin = 0.3, NetMargin = 0.1,
            RevenueYoY = 0.05, NetProfitYoY = 0.05, Eps = 1,
        };
        Assert.True(Pass(ok, f));
    }

    [Fact]
    public void PassValuation_AllRules()
    {
        var f = new ScreenerFilter { PeMax = 20, PbMax = 3, PsMax = 5, DividendYieldMin = 1 };
        Assert.False(PassValuation(new ScreenerRow(), f));        // 全部缺失
        Assert.False(PassValuation(new ScreenerRow { PE = -1 }, f));  // 负 PE
        Assert.True(PassValuation(new ScreenerRow
        { PE = 10, PB = 2, PS = 3, DividendYield = 2 }, f));
        Assert.False(PassValuation(new ScreenerRow
        { PE = 10, PB = 2, PS = 3, DividendYield = 0.5 }, f));      // 股息率太低
    }

    [Fact]
    public async Task ScreenAsync_Throws_WhenReportDateMissing()
    {
        var svc = new ScreenerService();
        await Assert.ThrowsAsync<ArgumentException>(() => svc.ScreenAsync(new ScreenerFilter()));
    }

    [Fact]
    public async Task ScreenAsync_FetchesAndFilters_WithStubHttp()
    {
        var listJson =
            "{\"result\":{\"data\":[" +
            "{\"SECURITY_CODE\":\"600000\",\"SECURITY_NAME_ABBR\":\"浦发银行\",\"PUBLISHNAME\":\"银行\"," +
            " \"BASIC_EPS\":1.2,\"TOTAL_OPERATE_INCOME\":1000,\"PARENT_NETPROFIT\":100," +
            " \"WEIGHTAVG_ROE\":15.0,\"XSMLL\":40.0,\"YSTZ\":12.0,\"SJLTZ\":8.0}" +
            "]}}";
        var emptyJson = "{\"result\":{\"data\":[]}}";
        var handler = new StubHttpMessageHandler((req, n) =>
            n == 1 ? StubHttpMessageHandler.Json(listJson) : StubHttpMessageHandler.Json(emptyJson));
        var svc = new ScreenerService(handler);
        var rows = await svc.ScreenAsync(new ScreenerFilter
        {
            ReportDate = "2024-12-31",
            IndustryContains = "银行",
        }, maxPages: 2);
        var row = Assert.Single(rows);
        Assert.Equal("600000", row.Code);
        Assert.Equal(0.15, row.Roe!.Value, 6);
        Assert.Equal(0.4, row.GrossMargin!.Value, 6);
    }
}
