using StockTechAnalyzer.Models;

namespace StockTechAnalyzer.Services;

/// <summary>
/// 行情数据源抽象。
/// </summary>
public interface IStockDataSource
{
    string Name { get; }

    /// <summary>按关键词搜索股票（代码/拼音/名称）。</summary>
    Task<IReadOnlyList<StockInfo>> SearchAsync(string keyword, CancellationToken ct = default);

    /// <summary>获取 K 线数据。返回按日期升序排列。</summary>
    Task<IReadOnlyList<Kline>> GetKlineAsync(StockInfo stock, KlinePeriod period, int count, CancellationToken ct = default);

    /// <summary>获取实时简要报价（最新价/涨跌幅）。可能返回 null。</summary>
    Task<RealtimeQuote?> GetQuoteAsync(StockInfo stock, CancellationToken ct = default);
}

public sealed class RealtimeQuote
{
    public string Name { get; set; } = "";
    public double Open { get; set; }
    public double PreClose { get; set; }
    public double Last { get; set; }
    public double High { get; set; }
    public double Low { get; set; }
    public double Volume { get; set; }
    public double Amount { get; set; }
    public double ChangePct => PreClose <= 0 ? 0 : (Last - PreClose) / PreClose * 100.0;
}
