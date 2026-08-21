namespace StockAnalyzer.Data;

/// <summary>同步时间戳表实体。</summary>
public sealed class SyncStamp
{
    public string Key { get; set; } = string.Empty;

    public DateTime UpdatedAt { get; set; }
}
