using ComparetoolWpf.Models;

namespace ComparetoolWpf.Services;

/// <summary>
/// 抽象股票数据源（搜索 + 三大报表）。所有实际实现都通过此接口对上层服务暴露。
/// 单一职责，不参与缓存（缓存在 <see cref="StockDataService"/>）。
/// </summary>
public interface IStockDataSource
{
    /// <summary>数据源人类可读名（"东方财富" / "雪球" / "新浪"）。日志、UI 显示用。</summary>
    string Name { get; }

    /// <summary>按关键字搜索 A 股。</summary>
    Task<List<StockInfo>> SearchStocksAsync(string keyword, CancellationToken ct = default);

    /// <summary>拉取指定股票指定报表的多期历史（按报告期倒序）。</summary>
    Task<List<FinancialReport>> GetReportsAsync(
        StockInfo stock,
        ReportKind kind,
        ReportPeriodType periodType = ReportPeriodType.All,
        int pageSize = 20,
        CancellationToken ct = default);
}
