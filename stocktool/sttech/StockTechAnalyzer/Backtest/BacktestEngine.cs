using StockTechAnalyzer.Indicators;
using StockTechAnalyzer.Models;

namespace StockTechAnalyzer.Backtest;

/// <summary>
/// 简易策略回测：每根 K 线收盘判断信号，次日开盘成交。
/// 全仓买入/全部卖出，不考虑滑点；手续费按双边费率参数计提。
/// </summary>
public static class BacktestEngine
{
    public enum Strategy
    {
        MacdCross,    // MACD 金叉买，死叉卖
        KdjCross,     // KDJ 金叉买，死叉卖
        Ma5Cross20,   // MA5 上穿 MA20 买，下穿卖
        BollBreak,    // 收盘突破上轨买，跌破中轨卖
        BuyAndHold,   // 基准
    }

    public sealed record Trade(DateTime BuyDate, double BuyPrice, DateTime SellDate, double SellPrice, double Return);
    public sealed record EquityPoint(DateTime Date, double Equity);

    public sealed record Result(
        Strategy Strategy,
        double InitCapital,
        double FinalEquity,
        double TotalReturn,
        double AnnualReturn,
        double WinRate,
        double MaxDrawdown,
        int TradeCount,
        IReadOnlyList<Trade> Trades,
        IReadOnlyList<EquityPoint> Equity);

    public static Result Run(IReadOnlyList<Kline> bars, Strategy strategy, double initCapital = 100000, double feeRate = 0.0003)
    {
        if (bars.Count < 30)
            return new Result(strategy, initCapital, initCapital, 0, 0, 0, 0, 0,
                Array.Empty<Trade>(), Array.Empty<EquityPoint>());

        var sigBuy = new bool[bars.Count];
        var sigSell = new bool[bars.Count];
        var closes = bars.Select(b => b.Close).ToArray();

        switch (strategy)
        {
            case Strategy.MacdCross:
            {
                var m = Indicators.Indicators.MACD(closes);
                for (int i = 1; i < bars.Count; i++)
                {
                    sigBuy[i]  = m.Hist[i] > 0 && m.Hist[i - 1] <= 0;
                    sigSell[i] = m.Hist[i] < 0 && m.Hist[i - 1] >= 0;
                }
                break;
            }
            case Strategy.KdjCross:
            {
                var k = Indicators.Indicators.KDJ(bars);
                for (int i = 1; i < bars.Count; i++)
                {
                    sigBuy[i]  = k.K[i] > k.D[i] && k.K[i - 1] <= k.D[i - 1];
                    sigSell[i] = k.K[i] < k.D[i] && k.K[i - 1] >= k.D[i - 1];
                }
                break;
            }
            case Strategy.Ma5Cross20:
            {
                var ma5 = Indicators.Indicators.SMA(closes, 5);
                var ma20 = Indicators.Indicators.SMA(closes, 20);
                for (int i = 1; i < bars.Count; i++)
                {
                    if (double.IsNaN(ma5[i]) || double.IsNaN(ma20[i]) || double.IsNaN(ma5[i-1]) || double.IsNaN(ma20[i-1])) continue;
                    sigBuy[i]  = ma5[i] > ma20[i] && ma5[i - 1] <= ma20[i - 1];
                    sigSell[i] = ma5[i] < ma20[i] && ma5[i - 1] >= ma20[i - 1];
                }
                break;
            }
            case Strategy.BollBreak:
            {
                var b = Indicators.Indicators.BOLL(closes);
                for (int i = 1; i < bars.Count; i++)
                {
                    if (double.IsNaN(b.Upper[i])) continue;
                    sigBuy[i]  = closes[i] > b.Upper[i] && closes[i - 1] <= b.Upper[i - 1];
                    sigSell[i] = closes[i] < b.Mid[i] && closes[i - 1] >= b.Mid[i - 1];
                }
                break;
            }
            case Strategy.BuyAndHold:
            {
                sigBuy[1] = true;
                break;
            }
        }

        return Simulate(bars, sigBuy, sigSell, strategy, initCapital, feeRate);
    }

    private static Result Simulate(IReadOnlyList<Kline> bars, bool[] sigBuy, bool[] sigSell,
        Strategy strategy, double initCapital, double feeRate)
    {
        double cash = initCapital;
        double shares = 0;
        bool holding = false;
        DateTime buyDate = default;
        double buyPrice = 0;
        var trades = new List<Trade>();
        var equity = new List<EquityPoint>(bars.Count);
        double peak = initCapital;
        double maxDd = 0;

        for (int i = 0; i < bars.Count; i++)
        {
            // 信号在收盘时产生，次日 open 成交
            if (i > 0)
            {
                double execPrice = bars[i].Open;
                if (!holding && sigBuy[i - 1] && execPrice > 0)
                {
                    shares = cash / execPrice * (1 - feeRate);
                    cash = 0;
                    holding = true;
                    buyDate = bars[i].Date;
                    buyPrice = execPrice;
                }
                else if (holding && sigSell[i - 1] && execPrice > 0)
                {
                    cash = shares * execPrice * (1 - feeRate);
                    var sellPrice = execPrice;
                    trades.Add(new Trade(buyDate, buyPrice, bars[i].Date, sellPrice, sellPrice / buyPrice - 1));
                    shares = 0;
                    holding = false;
                }
            }
            double eq = cash + shares * bars[i].Close;
            if (eq > peak) peak = eq;
            double dd = eq / peak - 1.0;
            if (dd < maxDd) maxDd = dd;
            equity.Add(new EquityPoint(bars[i].Date, eq));
        }
        // 区间末强平用于统计
        if (holding && shares > 0)
        {
            var last = bars[^1];
            double sellPrice = last.Close;
            cash = shares * sellPrice * (1 - feeRate);
            trades.Add(new Trade(buyDate, buyPrice, last.Date, sellPrice, sellPrice / buyPrice - 1));
        }
        double finalEq = equity[^1].Equity;
        double totalRet = finalEq / initCapital - 1.0;
        double days = (bars[^1].Date - bars[0].Date).TotalDays;
        double annRet = days > 0 ? Math.Pow(1 + totalRet, 365.0 / days) - 1.0 : totalRet;
        int wins = trades.Count(t => t.Return > 0);
        double winRate = trades.Count > 0 ? (double)wins / trades.Count : 0;

        return new Result(strategy, initCapital, finalEq, totalRet, annRet, winRate, maxDd,
            trades.Count, trades, equity);
    }
}
