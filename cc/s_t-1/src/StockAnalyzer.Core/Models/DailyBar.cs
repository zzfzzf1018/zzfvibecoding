namespace StockAnalyzer.Core.Models;

/// <summary>日线数据（不复权）。</summary>
public sealed class DailyBar
{
    public string Code { get; set; } = string.Empty;

    public MarketType Market { get; set; }

    public DateTime Date { get; set; }

    public double Open { get; set; }

    public double Close { get; set; }

    public double High { get; set; }

    public double Low { get; set; }

    public double Volume { get; set; }

    public double Amount { get; set; }

    public double ChangePercent { get; set; }

    public double TurnoverRate { get; set; }
}
