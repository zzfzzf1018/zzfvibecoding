using StockAnalyzer.Core.Models;

namespace StockAnalyzer.Core.Abstractions;

/// <summary>行情数据源抽象。可替换为东财 / 新浪 / 腾讯 等实现。</summary>
public interface IStockDataSource
{
    /// <summary>数据源名称。</summary>
    string Name { get; }

    /// <summary>拉取全市场股票列表（代码 + 名称）。属于大批量接口，可能被数据源限流。</summary>
    Task<IReadOnlyList<StockInfo>> GetStockListAsync(
        MarketGroup group,
        CancellationToken cancellationToken = default);

    /// <summary>在线模糊检索（代码 / 名称 / 拼音首字母），用于本地列表缺失时兜底。</summary>
    Task<IReadOnlyList<StockInfo>> SearchAsync(
        string keyword,
        MarketGroup group,
        int maxResults,
        CancellationToken cancellationToken = default);

    /// <summary>拉取单只股票的实时快照（价格、市值、PE、PB 等）。</summary>
    Task<StockQuote?> GetQuoteAsync(
        StockInfo stock,
        CancellationToken cancellationToken = default);

    /// <summary>批量拉取实时快照，用于自选股列表刷新。</summary>
    Task<IReadOnlyList<StockQuote>> GetQuotesAsync(
        IReadOnlyList<StockInfo> stocks,
        CancellationToken cancellationToken = default);

    /// <summary>拉取日线历史（不复权），用于估值序列计算。</summary>
    Task<IReadOnlyList<DailyBar>> GetDailyHistoryAsync(
        StockInfo stock,
        DateTime start,
        DateTime end,
        CancellationToken cancellationToken = default);

    /// <summary>拉取定期报告每股指标；数据源不支持时返回空列表。</summary>
    Task<IReadOnlyList<FinancialReport>> GetFinancialReportsAsync(
        StockInfo stock,
        CancellationToken cancellationToken = default);
}
