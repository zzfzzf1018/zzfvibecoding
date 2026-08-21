using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StockAnalyzer.Core.Abstractions;
using StockAnalyzer.Core.Models;
using StockAnalyzer.Core.Utils;

namespace StockAnalyzer.DataSource.Eastmoney;

/// <summary>
/// 基于东方财富公开行情接口的数据源实现。
/// </summary>
/// <remarks>
/// 字段口径（fltt=2 时接口直接返回浮点数，无需二次缩放）：
/// f2 最新价 / f3 涨跌幅% / f4 涨跌额 / f5 成交量 / f6 成交额 / f7 振幅% / f8 换手率% /
/// f9 市盈率(动态，最新报告期年化) / f12 代码 / f13 市场 / f14 名称 / f15 最高 / f16 最低 /
/// f17 今开 / f18 昨收 / f20 总市值 / f21 流通市值 / f23 市净率 /
/// f114 市盈率(静态) / f115 市盈率(TTM)。
/// 上述口径已用贵州茅台、工商银行、腾讯控股实际回测验证（f115 与自行按财报计算的 TTM 一致）。
/// </remarks>
public sealed class EastmoneyStockDataSource : IStockDataSource
{
    private readonly EastmoneyHttpClient _client;
    private readonly ILogger<EastmoneyStockDataSource> _logger;
    private readonly DataSourceOptions _options;

    public EastmoneyStockDataSource(
        EastmoneyHttpClient client,
        IOptions<DataSourceOptions> options,
        ILogger<EastmoneyStockDataSource> logger)
    {
        _client = client;
        _options = options.Value;
        _logger = logger;
    }

    public string Name => "东方财富（Eastmoney 公开行情接口）";

    // ------------------------------------------------------------------
    // 股票列表
    // ------------------------------------------------------------------

    public async Task<IReadOnlyList<StockInfo>> GetStockListAsync(
        MarketGroup group,
        CancellationToken cancellationToken = default)
    {
        var result = new List<StockInfo>();

        foreach (string filter in ResolveFilters(group))
        {
            result.AddRange(await FetchListAsync(filter, cancellationToken));
        }

        return result
            .GroupBy(s => (s.Market, s.Code))
            .Select(g => g.First())
            .ToList();
    }

    private static IEnumerable<string> ResolveFilters(MarketGroup group) => group switch
    {
        MarketGroup.AShare => new[] { EastmoneyEndpoints.FilterAShare },
        MarketGroup.HongKong => new[] { EastmoneyEndpoints.FilterHongKong },
        _ => new[] { EastmoneyEndpoints.FilterAShare, EastmoneyEndpoints.FilterHongKong }
    };

    private async Task<List<StockInfo>> FetchListAsync(string filter, CancellationToken cancellationToken)
    {
        var stocks = new List<StockInfo>();
        int pageSize = Math.Clamp(_options.ListPageSize, 100, 5000);
        int page = 1;

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            string url = $"{EastmoneyEndpoints.QuoteHost}{EastmoneyEndpoints.ClistPath}" +
                         $"?pn={page}&pz={pageSize}&po=1&np=1&fltt=2&invt=2&fid=f12" +
                         $"&fs={filter}&fields={EastmoneyEndpoints.ListFields}";

            using JsonDocument? document = await _client.GetJsonAsync(url, cancellationToken);
            if (document is null)
            {
                break;
            }

            if (!document.RootElement.TryGetProperty("data", out JsonElement data) ||
                data.ValueKind != JsonValueKind.Object)
            {
                break;
            }

            int total = data.GetInt32OrNull("total") ?? 0;

            if (!data.TryGetProperty("diff", out JsonElement diff))
            {
                break;
            }

            int countThisPage = 0;

            foreach (JsonElement item in EnumerateDiff(diff))
            {
                string? code = item.GetStringOrNull("f12");
                string? name = item.GetStringOrNull("f14");
                int marketId = item.GetInt32OrNull("f13") ?? -1;

                if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(name))
                {
                    continue;
                }

                MarketType market = SecurityIdHelper.FromEastmoneyMarketId(marketId, code);

                stocks.Add(new StockInfo
                {
                    Code = code,
                    Name = name.Trim(),
                    Market = market,
                    SecId = SecurityIdHelper.BuildSecId(market, code),
                    NameInitials = PinyinHelper.GetInitials(name),
                    UpdatedAt = DateTime.Now
                });

                countThisPage++;
            }

            if (countThisPage == 0 || stocks.Count >= total || page * pageSize >= total)
            {
                break;
            }

            page++;

            // clist 属于大批量接口，连续快速请求易触发服务端限流，这里主动降速
            await Task.Delay(800, cancellationToken);
        }

        _logger.LogInformation("已抓取 {Count} 条代码（筛选条件 {Filter}）。", stocks.Count, filter);
        return stocks;
    }

    private static IEnumerable<JsonElement> EnumerateDiff(JsonElement diff)
    {
        // np=1 时返回数组，个别情况下返回以序号为键的对象，这里同时兼容
        if (diff.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement item in diff.EnumerateArray())
            {
                yield return item;
            }
        }
        else if (diff.ValueKind == JsonValueKind.Object)
        {
            foreach (JsonProperty property in diff.EnumerateObject())
            {
                yield return property.Value;
            }
        }
    }

    // ------------------------------------------------------------------
    // 在线模糊检索
    // ------------------------------------------------------------------

    /// <summary>
    /// 基于东财搜索建议接口。支持代码、中文名称与拼音首字母，且直接返回 QuoteID（即 secid）。
    /// </summary>
    public async Task<IReadOnlyList<StockInfo>> SearchAsync(
        string keyword,
        MarketGroup group,
        int maxResults,
        CancellationToken cancellationToken = default)
    {
        keyword = keyword?.Trim() ?? string.Empty;

        if (keyword.Length == 0)
        {
            return Array.Empty<StockInfo>();
        }

        int count = Math.Clamp(maxResults, 1, 50);

        string url = $"{EastmoneyEndpoints.SearchHost}{EastmoneyEndpoints.SuggestPath}" +
                     $"?input={Uri.EscapeDataString(keyword)}&type=14" +
                     $"&token={EastmoneyEndpoints.SuggestToken}&count={count}";

        using JsonDocument? document = await _client.GetJsonAsync(url, cancellationToken);

        if (document is null ||
            !document.RootElement.TryGetProperty("QuotationCodeTable", out JsonElement table) ||
            !table.TryGetProperty("Data", out JsonElement data) ||
            data.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<StockInfo>();
        }

        var results = new List<StockInfo>();

        foreach (JsonElement item in data.EnumerateArray())
        {
            string? classify = item.GetStringOrNull("Classify");
            string? code = item.GetStringOrNull("Code");
            string? name = item.GetStringOrNull("Name");

            if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(name))
            {
                continue;
            }

            // 只保留 A 股与港股个股，排除指数、板块、美股、基金等
            MarketType market = classify switch
            {
                "AStock" => SecurityIdHelper.FromEastmoneyMarketId(item.GetInt32OrNull("MktNum") ?? -1, code),
                "HK" => MarketType.HongKong,
                _ => MarketType.Unknown
            };

            if (market == MarketType.Unknown || !MatchesGroup(market, group))
            {
                continue;
            }

            results.Add(new StockInfo
            {
                Code = code,
                Name = name.Trim(),
                Market = market,
                SecId = item.GetStringOrNull("QuoteID") ?? SecurityIdHelper.BuildSecId(market, code),
                NameInitials = item.GetStringOrNull("PinYin")?.ToUpperInvariant()
                               ?? PinyinHelper.GetInitials(name),
                UpdatedAt = DateTime.Now
            });
        }

        return results;
    }

    private static bool MatchesGroup(MarketType market, MarketGroup group) => group switch
    {
        MarketGroup.AShare => market != MarketType.HongKong,
        MarketGroup.HongKong => market == MarketType.HongKong,
        _ => true
    };

    // ------------------------------------------------------------------
    // 实时快照
    // ------------------------------------------------------------------

    public async Task<StockQuote?> GetQuoteAsync(StockInfo stock, CancellationToken cancellationToken = default)
    {
        IReadOnlyList<StockQuote> quotes = await GetQuotesAsync(new[] { stock }, cancellationToken);
        return quotes.FirstOrDefault();
    }

    public async Task<IReadOnlyList<StockQuote>> GetQuotesAsync(
        IReadOnlyList<StockInfo> stocks,
        CancellationToken cancellationToken = default)
    {
        if (stocks.Count == 0)
        {
            return Array.Empty<StockQuote>();
        }

        var quotes = new List<StockQuote>(stocks.Count);
        int batchSize = Math.Clamp(_options.QuoteBatchSize, 1, 100);

        foreach (StockInfo[] batch in stocks.Chunk(batchSize))
        {
            cancellationToken.ThrowIfCancellationRequested();

            string secIds = string.Join(',', batch.Select(s =>
                string.IsNullOrEmpty(s.SecId) ? SecurityIdHelper.BuildSecId(s.Market, s.Code) : s.SecId));

            string url = $"{EastmoneyEndpoints.QuoteHost}{EastmoneyEndpoints.UlistPath}" +
                         $"?fltt=2&invt=2&secids={secIds}&fields={EastmoneyEndpoints.QuoteFields}";

            using JsonDocument? document = await _client.GetJsonAsync(url, cancellationToken);
            if (document is null ||
                !document.RootElement.TryGetProperty("data", out JsonElement data) ||
                data.ValueKind != JsonValueKind.Object ||
                !data.TryGetProperty("diff", out JsonElement diff))
            {
                continue;
            }

            foreach (JsonElement item in EnumerateDiff(diff))
            {
                StockQuote? quote = ParseQuote(item);
                if (quote is not null)
                {
                    quotes.Add(quote);
                }
            }
        }

        return quotes;
    }

    private static StockQuote? ParseQuote(JsonElement item)
    {
        string? code = item.GetStringOrNull("f12");
        if (string.IsNullOrWhiteSpace(code))
        {
            return null;
        }

        int marketId = item.GetInt32OrNull("f13") ?? -1;
        MarketType market = SecurityIdHelper.FromEastmoneyMarketId(marketId, code);

        double? price = item.GetDoubleOrNull("f2");
        double? pb = item.GetDoubleOrNull("f23").NullIfZero();
        double? totalCap = item.GetDoubleOrNull("f20").NullIfZero();

        var quote = new StockQuote
        {
            Code = code,
            Market = market,
            Name = item.GetStringOrNull("f14")?.Trim() ?? string.Empty,
            Price = price,
            ChangePercent = item.GetDoubleOrNull("f3"),
            Change = item.GetDoubleOrNull("f4"),
            Volume = item.GetDoubleOrNull("f5"),
            Turnover = item.GetDoubleOrNull("f6"),
            Amplitude = item.GetDoubleOrNull("f7"),
            TurnoverRate = item.GetDoubleOrNull("f8"),
            PeDynamic = item.GetDoubleOrNull("f9").NullIfZero(),
            High = item.GetDoubleOrNull("f15"),
            Low = item.GetDoubleOrNull("f16"),
            Open = item.GetDoubleOrNull("f17"),
            PreviousClose = item.GetDoubleOrNull("f18"),
            TotalMarketCap = totalCap,
            CirculatingMarketCap = item.GetDoubleOrNull("f21").NullIfZero(),
            Pb = pb,
            PeStatic = item.GetDoubleOrNull("f114").NullIfZero(),
            PeTtm = item.GetDoubleOrNull("f115").NullIfZero(),
            CapturedAt = DateTime.Now
        };

        // 接口不直接提供每股净资产与股本，这里由价格与倍数反算，保证内部口径自洽
        if (price is > 0 && pb is > 0)
        {
            quote.BookValuePerShare = price / pb;
        }

        if (price is > 0 && totalCap is > 0)
        {
            quote.TotalShares = totalCap / price;
        }

        if (price is > 0 && quote.CirculatingMarketCap is > 0)
        {
            quote.CirculatingShares = quote.CirculatingMarketCap / price;
        }

        return quote;
    }

    // ------------------------------------------------------------------
    // 历史日线
    // ------------------------------------------------------------------

    public async Task<IReadOnlyList<DailyBar>> GetDailyHistoryAsync(
        StockInfo stock,
        DateTime start,
        DateTime end,
        CancellationToken cancellationToken = default)
    {
        string secId = string.IsNullOrEmpty(stock.SecId)
            ? SecurityIdHelper.BuildSecId(stock.Market, stock.Code)
            : stock.SecId;

        // klt=101 日线；fqt=0 不复权（与财报每股指标口径一致）
        string url = $"{EastmoneyEndpoints.HistoryHost}{EastmoneyEndpoints.KlinePath}" +
                     $"?secid={secId}&klt=101&fqt=0" +
                     $"&fields1={EastmoneyEndpoints.KlineFields1}&fields2={EastmoneyEndpoints.KlineFields2}" +
                     $"&beg={start:yyyyMMdd}&end={end:yyyyMMdd}&lmt=1000000";

        using JsonDocument? document = await _client.GetJsonAsync(url, cancellationToken);

        if (document is null ||
            !document.RootElement.TryGetProperty("data", out JsonElement data) ||
            data.ValueKind != JsonValueKind.Object ||
            !data.TryGetProperty("klines", out JsonElement klines) ||
            klines.ValueKind != JsonValueKind.Array)
        {
            _logger.LogWarning("未取得 {Code} 的日线数据。", stock.Code);
            return Array.Empty<DailyBar>();
        }

        var bars = new List<DailyBar>(klines.GetArrayLength());

        foreach (JsonElement line in klines.EnumerateArray())
        {
            DailyBar? bar = ParseKline(line.GetString(), stock);
            if (bar is not null)
            {
                bars.Add(bar);
            }
        }

        bars.Sort((a, b) => a.Date.CompareTo(b.Date));
        return bars;
    }

    /// <summary>K 线格式：日期,开,收,高,低,成交量,成交额,振幅,涨跌幅,涨跌额,换手率。</summary>
    private static DailyBar? ParseKline(string? raw, StockInfo stock)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        string[] parts = raw.Split(',');
        if (parts.Length < 11)
        {
            return null;
        }

        if (!DateTime.TryParse(parts[0], CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime date))
        {
            return null;
        }

        return new DailyBar
        {
            Code = stock.Code,
            Market = stock.Market,
            Date = date.Date,
            Open = ParseDouble(parts[1]),
            Close = ParseDouble(parts[2]),
            High = ParseDouble(parts[3]),
            Low = ParseDouble(parts[4]),
            Volume = ParseDouble(parts[5]),
            Amount = ParseDouble(parts[6]),
            ChangePercent = ParseDouble(parts[8]),
            TurnoverRate = ParseDouble(parts[10])
        };
    }

    private static double ParseDouble(string text) =>
        double.TryParse(text, NumberStyles.Any, CultureInfo.InvariantCulture, out double value) ? value : 0d;

    // ------------------------------------------------------------------
    // 定期报告
    // ------------------------------------------------------------------

    public async Task<IReadOnlyList<FinancialReport>> GetFinancialReportsAsync(
        StockInfo stock,
        CancellationToken cancellationToken = default)
    {
        return stock.Market == MarketType.HongKong
            ? await GetHongKongReportsAsync(stock, cancellationToken)
            : await GetAShareReportsAsync(stock, cancellationToken);
    }

    private async Task<IReadOnlyList<FinancialReport>> GetAShareReportsAsync(
        StockInfo stock,
        CancellationToken cancellationToken)
    {
        string filter = Uri.EscapeDataString($"(SECURITY_CODE=\"{stock.Code}\")");

        string url = $"{EastmoneyEndpoints.DataCenterHost}{EastmoneyEndpoints.DataCenterPath}" +
                     $"?reportName={EastmoneyEndpoints.FinanceReportName}" +
                     $"&columns={EastmoneyEndpoints.FinanceColumns}" +
                     $"&filter={filter}" +
                     "&sortColumns=REPORTDATE&sortTypes=-1&pageNumber=1&pageSize=200&source=DataCenter&client=WEB";

        using JsonDocument? document = await _client.GetJsonAsync(url, cancellationToken);

        if (document is null ||
            !document.RootElement.TryGetProperty("result", out JsonElement resultElement) ||
            resultElement.ValueKind != JsonValueKind.Object ||
            !resultElement.TryGetProperty("data", out JsonElement dataElement) ||
            dataElement.ValueKind != JsonValueKind.Array)
        {
            _logger.LogWarning("未取得 {Code} 的财报数据，估值序列将退化为近似模式。", stock.Code);
            return Array.Empty<FinancialReport>();
        }

        var reports = new List<FinancialReport>(dataElement.GetArrayLength());

        foreach (JsonElement item in dataElement.EnumerateArray())
        {
            DateTime? reportDate = item.GetDateOrNull("REPORTDATE");
            if (reportDate is null)
            {
                continue;
            }

            reports.Add(new FinancialReport
            {
                Code = stock.Code,
                Market = stock.Market,
                ReportDate = reportDate.Value,
                NoticeDate = item.GetDateOrNull("NOTICE_DATE") ?? reportDate.Value.AddDays(45),
                BasicEps = item.GetDoubleOrNull("BASIC_EPS"),
                BookValuePerShare = item.GetDoubleOrNull("BPS"),
                NetProfit = item.GetDoubleOrNull("PARENT_NETPROFIT"),
                WeightedRoe = item.GetDoubleOrNull("WEIGHTAVG_ROE")
            });
        }

        _logger.LogInformation("已取得 {Code} 的 {Count} 期财报。", stock.Code, reports.Count);
        return reports.OrderBy(r => r.ReportDate).ToList();
    }

    /// <summary>
    /// 港股主要财务指标。该接口直接给出 EPS_TTM 与 BPS，无需自行还原滚动口径；
    /// 但报表币种可能不是港元，因此依赖上层的快照校准步骤对齐量纲。
    /// </summary>
    private async Task<IReadOnlyList<FinancialReport>> GetHongKongReportsAsync(
        StockInfo stock,
        CancellationToken cancellationToken)
    {
        string secuCode = SecurityIdHelper.BuildSecuCode(stock.Market, stock.Code);
        string filter = Uri.EscapeDataString($"(SECUCODE=\"{secuCode}\")");

        string url = $"{EastmoneyEndpoints.DataCenterHost}{EastmoneyEndpoints.DataCenterPath}" +
                     $"?reportName={EastmoneyEndpoints.HongKongFinanceReportName}" +
                     $"&columns={EastmoneyEndpoints.HongKongFinanceColumns}" +
                     $"&filter={filter}" +
                     "&sortColumns=STD_REPORT_DATE&sortTypes=-1&pageNumber=1&pageSize=200&source=F10&client=PC";

        using JsonDocument? document = await _client.GetJsonAsync(url, cancellationToken);

        if (document is null ||
            !document.RootElement.TryGetProperty("result", out JsonElement resultElement) ||
            resultElement.ValueKind != JsonValueKind.Object ||
            !resultElement.TryGetProperty("data", out JsonElement dataElement) ||
            dataElement.ValueKind != JsonValueKind.Array)
        {
            _logger.LogWarning("未取得港股 {Code} 的财务指标，估值序列将退化为近似模式。", stock.Code);
            return Array.Empty<FinancialReport>();
        }

        var reports = new List<FinancialReport>(dataElement.GetArrayLength());

        foreach (JsonElement item in dataElement.EnumerateArray())
        {
            DateTime? reportDate = item.GetDateOrNull("REPORT_DATE");
            if (reportDate is null)
            {
                continue;
            }

            reports.Add(new FinancialReport
            {
                Code = stock.Code,
                Market = stock.Market,
                ReportDate = reportDate.Value,
                NoticeDate = reportDate.Value.AddDays(EastmoneyEndpoints.HongKongNoticeLagDays),
                BasicEps = item.GetDoubleOrNull("BASIC_EPS"),
                EpsTtm = item.GetDoubleOrNull("EPS_TTM"),
                BookValuePerShare = item.GetDoubleOrNull("BPS"),
                NetProfit = item.GetDoubleOrNull("HOLDER_PROFIT"),
                WeightedRoe = item.GetDoubleOrNull("ROE_AVG")
            });
        }

        _logger.LogInformation("已取得港股 {Code} 的 {Count} 期财务指标。", stock.Code, reports.Count);
        return reports.OrderBy(r => r.ReportDate).ToList();
    }
}
