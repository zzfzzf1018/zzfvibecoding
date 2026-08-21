namespace StockAnalyzer.DataSource;

/// <summary>数据源可调参数。</summary>
public sealed class DataSourceOptions
{
    public const string SectionName = "DataSource";

    /// <summary>单次网络请求超时（秒）。</summary>
    public int TimeoutSeconds { get; set; } = 20;

    /// <summary>失败重试次数。</summary>
    public int RetryCount { get; set; } = 2;

    /// <summary>重试基础退避（毫秒），按次数线性放大。</summary>
    public int RetryBaseDelayMilliseconds { get; set; } = 400;

    /// <summary>并发请求上限，避免触发数据源限流。</summary>
    public int MaxConcurrentRequests { get; set; } = 4;

    /// <summary>列表接口分页大小。</summary>
    public int ListPageSize { get; set; } = 1000;

    /// <summary>批量快照单次请求的最大股票数。</summary>
    public int QuoteBatchSize { get; set; } = 50;
}
