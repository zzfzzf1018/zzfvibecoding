namespace StockAnalyzer.Core.Models;

/// <summary>股票静态信息（代码、名称、市场）。</summary>
public sealed class StockInfo
{
    /// <summary>不含市场前缀的原始代码，如 600519 / 00700。</summary>
    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public MarketType Market { get; set; }

    /// <summary>东方财富 secid，如 1.600519、0.000001、116.00700。</summary>
    public string SecId { get; set; } = string.Empty;

    /// <summary>名称拼音首字母（大写），用于模糊检索，可能为空。</summary>
    public string? NameInitials { get; set; }

    public DateTime UpdatedAt { get; set; }

    /// <summary>唯一键：市场 + 代码。</summary>
    public string Key => BuildKey(Market, Code);

    public static string BuildKey(MarketType market, string code) => $"{(int)market}:{code}";

    public string DisplayText => $"{Code} {Name} [{Market.ToDisplayName()}]";

    public override string ToString() => DisplayText;
}
