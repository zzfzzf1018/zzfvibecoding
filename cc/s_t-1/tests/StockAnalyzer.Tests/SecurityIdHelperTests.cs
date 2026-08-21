using StockAnalyzer.Core.Models;
using StockAnalyzer.Core.Utils;
using Xunit;

namespace StockAnalyzer.Tests;

public class SecurityIdHelperTests
{
    [Theory]
    [InlineData("600519", MarketType.ShanghaiA)]
    [InlineData("688111", MarketType.ShanghaiA)]
    [InlineData("000001", MarketType.ShenzhenA)]
    [InlineData("300750", MarketType.ShenzhenA)]
    [InlineData("430047", MarketType.BeijingA)]
    [InlineData("920992", MarketType.BeijingA)]
    [InlineData("00700", MarketType.HongKong)]
    public void InferMarket_HandlesCommonCodes(string code, MarketType expected)
        => Assert.Equal(expected, SecurityIdHelper.InferMarket(code));

    [Theory]
    [InlineData(MarketType.ShanghaiA, "600519", "1.600519")]
    [InlineData(MarketType.ShenzhenA, "000001", "0.000001")]
    [InlineData(MarketType.BeijingA, "430047", "0.430047")]
    [InlineData(MarketType.HongKong, "00700", "116.00700")]
    public void BuildSecId_MatchesEastmoneyFormat(MarketType market, string code, string expected)
        => Assert.Equal(expected, SecurityIdHelper.BuildSecId(market, code));

    [Theory]
    [InlineData("sh600519", "600519")]
    [InlineData("600519.SH", "600519")]
    [InlineData("1.600519", "600519")]
    [InlineData("  00700 ", "00700")]
    public void Normalize_StripsPrefixesAndSuffixes(string input, string expected)
        => Assert.Equal(expected, SecurityIdHelper.Normalize(input));

    [Fact]
    public void FromEastmoneyMarketId_MapsHongKongVariants()
    {
        Assert.Equal(MarketType.HongKong, SecurityIdHelper.FromEastmoneyMarketId(116, "00700"));
        Assert.Equal(MarketType.HongKong, SecurityIdHelper.FromEastmoneyMarketId(128, "00700"));
    }

    [Fact]
    public void BuildSecuCode_AppendsExchangeSuffix()
    {
        Assert.Equal("600519.SH", SecurityIdHelper.BuildSecuCode(MarketType.ShanghaiA, "600519"));
        Assert.Equal("00700.HK", SecurityIdHelper.BuildSecuCode(MarketType.HongKong, "00700"));
    }
}

public class PinyinHelperTests
{
    [Theory]
    [InlineData("贵州茅台", "GZMT")]
    [InlineData("平安银行", "PAYH")]
    [InlineData("中国平安", "ZGPA")]
    public void GetInitials_ExtractsPinyinInitials(string name, string expected)
        => Assert.Equal(expected, PinyinHelper.GetInitials(name));

    [Fact]
    public void GetInitials_KeepsAsciiAndDropsSymbols()
        => Assert.Equal("STSM", PinyinHelper.GetInitials("*ST数码"));

    [Fact]
    public void GetInitials_ReturnsEmptyForBlankInput()
        => Assert.Equal(string.Empty, PinyinHelper.GetInitials("   "));
}
