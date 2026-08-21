namespace StockAnalyzer.Core.Models;

/// <summary>交易市场。</summary>
public enum MarketType
{
    /// <summary>未知。</summary>
    Unknown = 0,

    /// <summary>上海证券交易所。</summary>
    ShanghaiA = 1,

    /// <summary>深圳证券交易所。</summary>
    ShenzhenA = 2,

    /// <summary>北京证券交易所。</summary>
    BeijingA = 3,

    /// <summary>香港交易所。</summary>
    HongKong = 4
}

/// <summary>市场分组，用于界面筛选。</summary>
public enum MarketGroup
{
    All = 0,
    AShare = 1,
    HongKong = 2
}

public static class MarketTypeExtensions
{
    public static MarketGroup ToGroup(this MarketType market) => market switch
    {
        MarketType.HongKong => MarketGroup.HongKong,
        MarketType.Unknown => MarketGroup.All,
        _ => MarketGroup.AShare
    };

    public static string ToDisplayName(this MarketType market) => market switch
    {
        MarketType.ShanghaiA => "沪市",
        MarketType.ShenzhenA => "深市",
        MarketType.BeijingA => "北交所",
        MarketType.HongKong => "港股",
        _ => "未知"
    };

    /// <summary>返回该市场的计价货币代码。</summary>
    public static string CurrencyCode(this MarketType market) =>
        market == MarketType.HongKong ? "HKD" : "CNY";
}
