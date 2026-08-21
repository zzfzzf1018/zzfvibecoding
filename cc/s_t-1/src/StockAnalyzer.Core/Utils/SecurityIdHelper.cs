using StockAnalyzer.Core.Models;

namespace StockAnalyzer.Core.Utils;

/// <summary>股票代码 &lt;-&gt; 东方财富 secid 的推断与转换。</summary>
public static class SecurityIdHelper
{
    /// <summary>东财行情市场号：0=深/北，1=沪，116=港。</summary>
    public const int MarketIdShenzhen = 0;
    public const int MarketIdShanghai = 1;
    public const int MarketIdBeijing = 0;
    public const int MarketIdHongKong = 116;

    /// <summary>把列表接口返回的 f13 映射为内部市场枚举。</summary>
    public static MarketType FromEastmoneyMarketId(int marketId, string code)
    {
        return marketId switch
        {
            1 => MarketType.ShanghaiA,
            116 or 128 => MarketType.HongKong,
            0 => IsBeijingCode(code) ? MarketType.BeijingA : MarketType.ShenzhenA,
            _ => InferMarket(code)
        };
    }

    /// <summary>仅凭代码推断市场（用于用户手工输入）。</summary>
    public static MarketType InferMarket(string code)
    {
        code = Normalize(code);

        if (code.Length == 5)
        {
            return MarketType.HongKong;
        }

        if (code.Length != 6)
        {
            return MarketType.Unknown;
        }

        // 北交所的 92xxxx 与沪市 B 股 900xxx 前缀相近，必须先判断北交所
        if (IsBeijingCode(code))
        {
            return MarketType.BeijingA;
        }

        if (code.StartsWith("6", StringComparison.Ordinal) ||
            code.StartsWith("900", StringComparison.Ordinal) ||
            code.StartsWith("5", StringComparison.Ordinal))
        {
            return MarketType.ShanghaiA;
        }

        return MarketType.ShenzhenA;
    }

    public static string BuildSecId(MarketType market, string code)
    {
        code = Normalize(code);

        int marketId = market switch
        {
            MarketType.ShanghaiA => MarketIdShanghai,
            MarketType.ShenzhenA => MarketIdShenzhen,
            MarketType.BeijingA => MarketIdBeijing,
            MarketType.HongKong => MarketIdHongKong,
            _ => InferMarket(code) == MarketType.ShanghaiA ? MarketIdShanghai : MarketIdShenzhen
        };

        return $"{marketId}.{code}";
    }

    /// <summary>财报接口使用的带交易所后缀代码，如 600519.SH。</summary>
    public static string BuildSecuCode(MarketType market, string code)
    {
        code = Normalize(code);

        string suffix = market switch
        {
            MarketType.ShanghaiA => "SH",
            MarketType.ShenzhenA => "SZ",
            MarketType.BeijingA => "BJ",
            MarketType.HongKong => "HK",
            _ => "SH"
        };

        return $"{code}.{suffix}";
    }

    /// <summary>标准化代码：去空格、大写、港股补齐 5 位。</summary>
    public static string Normalize(string code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return string.Empty;
        }

        code = code.Trim().ToUpperInvariant();

        int dot = code.IndexOf('.');
        if (dot > 0)
        {
            // 兼容 600519.SH / 1.600519 两种写法
            string left = code[..dot];
            string right = code[(dot + 1)..];
            code = left.Length >= right.Length ? left : right;
        }

        if (code.StartsWith("SH", StringComparison.Ordinal) ||
            code.StartsWith("SZ", StringComparison.Ordinal) ||
            code.StartsWith("BJ", StringComparison.Ordinal) ||
            code.StartsWith("HK", StringComparison.Ordinal))
        {
            code = code[2..];
        }

        return code;
    }

    private static bool IsBeijingCode(string code) =>
        code.Length == 6 &&
        (code.StartsWith("43", StringComparison.Ordinal) ||
         code.StartsWith("83", StringComparison.Ordinal) ||
         code.StartsWith("87", StringComparison.Ordinal) ||
         code.StartsWith("88", StringComparison.Ordinal) ||
         code.StartsWith("92", StringComparison.Ordinal));
}
