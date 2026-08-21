using System.Net;
using Microsoft.Extensions.DependencyInjection;
using StockAnalyzer.Core.Abstractions;
using StockAnalyzer.DataSource.Eastmoney;

namespace StockAnalyzer.DataSource;

public static class DataSourceServiceCollectionExtensions
{
    /// <summary>注册东方财富数据源及其 HttpClient。</summary>
    public static IServiceCollection AddEastmoneyDataSource(
        this IServiceCollection services,
        Action<DataSourceOptions>? configure = null)
    {
        services.AddOptions<DataSourceOptions>().Configure(options => configure?.Invoke(options));

        services.AddHttpClient<EastmoneyHttpClient>((provider, client) =>
        {
            var options = provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<DataSourceOptions>>().Value;

            client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
            client.DefaultRequestHeaders.UserAgent.ParseAdd(
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) " +
                "Chrome/124.0.0.0 Safari/537.36");
            client.DefaultRequestHeaders.Accept.ParseAdd("application/json, text/javascript, */*; q=0.01");
            client.DefaultRequestHeaders.Referrer = new Uri("https://quote.eastmoney.com/");
        })
        .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
        {
            AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate | DecompressionMethods.Brotli
        });

        services.AddSingleton<IStockDataSource, EastmoneyStockDataSource>();

        return services;
    }
}
