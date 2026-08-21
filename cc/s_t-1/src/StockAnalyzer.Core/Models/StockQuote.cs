namespace StockAnalyzer.Core.Models;

/// <summary>个股实时行情与基本面快照。</summary>
public sealed class StockQuote
{
    public string Code { get; set; } = string.Empty;

    public MarketType Market { get; set; }

    public string Name { get; set; } = string.Empty;

    /// <summary>最新价。</summary>
    public double? Price { get; set; }

    /// <summary>昨收。</summary>
    public double? PreviousClose { get; set; }

    public double? Open { get; set; }

    public double? High { get; set; }

    public double? Low { get; set; }

    /// <summary>涨跌额。</summary>
    public double? Change { get; set; }

    /// <summary>涨跌幅（%）。</summary>
    public double? ChangePercent { get; set; }

    /// <summary>振幅（%）。</summary>
    public double? Amplitude { get; set; }

    /// <summary>成交量（股）。</summary>
    public double? Volume { get; set; }

    /// <summary>成交额（元 / 港元）。</summary>
    public double? Turnover { get; set; }

    /// <summary>换手率（%）。</summary>
    public double? TurnoverRate { get; set; }

    /// <summary>总市值。</summary>
    public double? TotalMarketCap { get; set; }

    /// <summary>流通市值。</summary>
    public double? CirculatingMarketCap { get; set; }

    /// <summary>市盈率（TTM，滚动 12 个月）。对应东财 f115。</summary>
    public double? PeTtm { get; set; }

    /// <summary>市盈率（静态，最近年报）。对应东财 f114。</summary>
    public double? PeStatic { get; set; }

    /// <summary>市盈率（动态，最新报告期年化）。对应东财 f9。</summary>
    public double? PeDynamic { get; set; }

    /// <summary>市净率。</summary>
    public double? Pb { get; set; }

    /// <summary>每股净资产。</summary>
    public double? BookValuePerShare { get; set; }

    /// <summary>净资产收益率（%）。</summary>
    public double? Roe { get; set; }

    /// <summary>总股本。</summary>
    public double? TotalShares { get; set; }

    /// <summary>流通股本。</summary>
    public double? CirculatingShares { get; set; }

    /// <summary>快照抓取时间（本地时间）。</summary>
    public DateTime CapturedAt { get; set; }

    public string Currency => Market.CurrencyCode();
}
