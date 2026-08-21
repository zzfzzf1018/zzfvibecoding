namespace StockAnalyzer.Core.Models;

/// <summary>自选股条目。</summary>
public sealed class WatchlistItem
{
    public string Code { get; set; } = string.Empty;

    public MarketType Market { get; set; }

    public string Name { get; set; } = string.Empty;

    /// <summary>排序序号，越小越靠前。</summary>
    public int SortOrder { get; set; }

    public string? Note { get; set; }

    public DateTime AddedAt { get; set; }

    public string Key => StockInfo.BuildKey(Market, Code);
}
