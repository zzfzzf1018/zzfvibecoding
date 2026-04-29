namespace StockTechAnalyzer.Models;

/// <summary>
/// 单根 K 线（OHLCV）。
/// </summary>
public sealed class Kline
{
    public DateTime Date { get; set; }
    public double Open { get; set; }
    public double High { get; set; }
    public double Low { get; set; }
    public double Close { get; set; }
    public double Volume { get; set; }   // 成交量（手）
    public double Amount { get; set; }   // 成交额（元，可能为 0）
}

public enum KlinePeriod
{
    Daily,
    Weekly,
    Monthly,
}

public sealed class StockInfo
{
    public string Code { get; set; } = "";        // 600000
    public string FullCode { get; set; } = "";    // sh600000
    public string Name { get; set; } = "";
    public string Market { get; set; } = "";      // sh / sz / bj
    public override string ToString() => $"{Code} {Name}";
}
