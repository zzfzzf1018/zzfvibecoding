using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace StockAnalyzer.DataSource.Eastmoney;

/// <summary>负责发起 HTTP 请求、重试与 JSON 解析的底层客户端。</summary>
public sealed class EastmoneyHttpClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<EastmoneyHttpClient> _logger;
    private readonly DataSourceOptions _options;
    private readonly SemaphoreSlim _throttle;

    public EastmoneyHttpClient(
        HttpClient httpClient,
        IOptions<DataSourceOptions> options,
        ILogger<EastmoneyHttpClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        _options = options.Value;
        _throttle = new SemaphoreSlim(Math.Max(1, _options.MaxConcurrentRequests));
    }

    /// <summary>
    /// 请求指定 URL 并返回解析后的 JSON 根节点；失败时返回 null（调用方决定降级策略）。
    /// </summary>
    public async Task<JsonDocument?> GetJsonAsync(string url, CancellationToken cancellationToken)
    {
        await _throttle.WaitAsync(cancellationToken);

        try
        {
            for (int attempt = 0; attempt <= _options.RetryCount; attempt++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    using HttpResponseMessage response =
                        await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

                    if (response.StatusCode is HttpStatusCode.TooManyRequests or HttpStatusCode.ServiceUnavailable)
                    {
                        _logger.LogWarning("数据源限流（{Status}），第 {Attempt} 次重试。", response.StatusCode, attempt + 1);
                        await DelayAsync(attempt, cancellationToken);
                        continue;
                    }

                    response.EnsureSuccessStatusCode();

                    await using Stream stream = await response.Content.ReadAsStreamAsync(cancellationToken);
                    return await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
                {
                    if (attempt == _options.RetryCount)
                    {
                        _logger.LogError(ex, "请求失败并已达到最大重试次数：{Url}", Sanitize(url));
                        return null;
                    }

                    _logger.LogWarning("请求失败（{Message}），准备第 {Attempt} 次重试。", ex.Message, attempt + 1);
                    await DelayAsync(attempt, cancellationToken);
                }
            }

            return null;
        }
        finally
        {
            _throttle.Release();
        }
    }

    private Task DelayAsync(int attempt, CancellationToken cancellationToken) =>
        Task.Delay(_options.RetryBaseDelayMilliseconds * (attempt + 1), cancellationToken);

    /// <summary>日志中只保留路径，避免打印过长查询串。</summary>
    private static string Sanitize(string url)
    {
        int index = url.IndexOf('?');
        return index > 0 ? url[..index] : url;
    }
}
