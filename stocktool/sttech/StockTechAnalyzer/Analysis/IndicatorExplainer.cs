using System.Text;
using StockTechAnalyzer.Indicators;
using StockTechAnalyzer.Models;

namespace StockTechAnalyzer.Analysis;

/// <summary>
/// 把各项技术指标当前数值翻译成"小白也能看懂"的人话解读。
/// 不输出投资建议，只解释含义与当前所处位置。
/// </summary>
public static class IndicatorExplainer
{
    public sealed record Item(string Name, string WhatIsIt, string CurrentReading);

    public static IReadOnlyList<Item> Explain(IReadOnlyList<Kline> bars, ChipDistribution.Result? chip)
    {
        var list = new List<Item>();
        if (bars.Count < 30) return list;

        var close = bars.Select(b => b.Close).ToArray();
        int n = close.Length;
        double last = close[^1];
        double prev = close[^2];
        double chgPct = (last - prev) / prev * 100;

        // ---------- 价格变化 ----------
        list.Add(new Item(
            "今日价格",
            "就是当前最新成交价，与昨天收盘价对比看涨跌幅。",
            $"现价 {last:F2}，较前一日 {(chgPct >= 0 ? "上涨" : "下跌")} {Math.Abs(chgPct):F2}%。"
        ));

        // ---------- MA 均线 ----------
        var ma5 = Indicators.Indicators.SMA(close, 5);
        var ma10 = Indicators.Indicators.SMA(close, 10);
        var ma20 = Indicators.Indicators.SMA(close, 20);
        var ma60 = Indicators.Indicators.SMA(close, 60);
        string maReading;
        if (!double.IsNaN(ma60[^1]))
        {
            string Pos(double m) => last > m ? "之上" : "之下";
            string trend;
            if (ma5[^1] > ma10[^1] && ma10[^1] > ma20[^1] && ma20[^1] > ma60[^1])
                trend = "短中长期均线全部向上排列（多头排列），说明各路资金都在赚钱，趋势偏强。";
            else if (ma5[^1] < ma10[^1] && ma10[^1] < ma20[^1] && ma20[^1] < ma60[^1])
                trend = "短中长期均线全部向下排列（空头排列），说明各路资金都在亏钱，趋势偏弱。";
            else
                trend = "均线交错，趋势不明朗，处于震荡或转折阶段。";
            maReading = $"价位于 MA5 {Pos(ma5[^1])}、MA20 {Pos(ma20[^1])}、MA60 {Pos(ma60[^1])}。{trend}";
        }
        else maReading = "数据不足以计算 MA60。";

        list.Add(new Item(
            "均线 MA",
            "把过去 N 天的收盘价平均一下连成线。MA5 看一周走势，MA20 看一个月，MA60 看一个季度。" +
            "价格在均线上方=偏强；下方=偏弱；多条均线由上到下依次为 MA5>MA10>MA20>MA60，叫\"多头排列\"，是强势信号。",
            maReading
        ));

        // ---------- MACD ----------
        var macd = Indicators.Indicators.MACD(close);
        double dif = macd.Dif[^1], dea = macd.Dea[^1], hist = macd.Hist[^1], histPrev = macd.Hist[^2];
        string macdReading;
        if (hist > 0 && histPrev <= 0)
            macdReading = $"刚刚\"金叉\"（红柱由无变有），是短期看多的转折信号。DIF={dif:F3}，DEA={dea:F3}。";
        else if (hist < 0 && histPrev >= 0)
            macdReading = $"刚刚\"死叉\"（绿柱由无变有），是短期看空的转折信号。DIF={dif:F3}，DEA={dea:F3}。";
        else if (hist > 0)
            macdReading = $"红柱{(hist > histPrev ? "在放大" : "在缩短")}，多头动能{(hist > histPrev ? "增强" : "减弱")}。DIF={dif:F3}，DEA={dea:F3}。";
        else
            macdReading = $"绿柱{(hist < histPrev ? "在放大" : "在缩短")}，空头动能{(hist < histPrev ? "增强" : "减弱")}。DIF={dif:F3}，DEA={dea:F3}。";

        list.Add(new Item(
            "MACD",
            "判断买卖力量强弱的指标。\"红柱\"代表多头力量，\"绿柱\"代表空头力量；红柱由无到有叫金叉（看多信号），" +
            "绿柱由无到有叫死叉（看空信号）。柱子越大表示力量越强。",
            macdReading
        ));

        // ---------- KDJ ----------
        var kdj = Indicators.Indicators.KDJ(bars);
        double k = kdj.K[^1], d = kdj.D[^1], j = kdj.J[^1];
        string kdjReading;
        if (j > 100) kdjReading = $"J 值 {j:F1}（>100），处于\"超买\"区域，意味着短期涨幅可能过大，警惕回调。";
        else if (j < 0) kdjReading = $"J 值 {j:F1}（<0），处于\"超卖\"区域，意味着短期跌幅可能过大，存在反弹机会。";
        else if (k > d && kdj.K[^2] <= kdj.D[^2]) kdjReading = $"K 线上穿 D 线（金叉），短期看多。K={k:F1}, D={d:F1}, J={j:F1}。";
        else if (k < d && kdj.K[^2] >= kdj.D[^2]) kdjReading = $"K 线下穿 D 线（死叉），短期看空。K={k:F1}, D={d:F1}, J={j:F1}。";
        else kdjReading = $"K={k:F1}, D={d:F1}, J={j:F1}，处于中性区域，无明显买卖信号。";

        list.Add(new Item(
            "KDJ",
            "短线敏感的超买超卖指标。K、D、J 三条线，数值范围一般在 0-100。" +
            "J 值 > 80 是超买（涨多了），< 20 是超卖（跌多了）。K 上穿 D 是金叉（买点），下穿是死叉（卖点）。",
            kdjReading
        ));

        // ---------- RSI ----------
        var rsiArr = Indicators.Indicators.RSI(close);
        double rsi = rsiArr[^1];
        string rsiReading = double.IsNaN(rsi) ? "数据不足。" :
            rsi > 80 ? $"RSI={rsi:F1}，严重超买，回调概率较大。" :
            rsi > 70 ? $"RSI={rsi:F1}，进入超买区，需警惕短期回落。" :
            rsi < 20 ? $"RSI={rsi:F1}，严重超卖，反弹概率较大。" :
            rsi < 30 ? $"RSI={rsi:F1}，进入超卖区，可关注是否企稳。" :
            $"RSI={rsi:F1}，处于中性区域（30-70），趋势温和。";

        list.Add(new Item(
            "RSI",
            "相对强弱指标，衡量\"近期上涨力量 vs 下跌力量\"的比例，0-100。" +
            "高于 70 算超买（短期涨多了），低于 30 算超卖（短期跌多了），50 附近表示多空均衡。",
            rsiReading
        ));

        // ---------- BOLL ----------
        var boll = Indicators.Indicators.BOLL(close);
        string bollReading;
        if (double.IsNaN(boll.Upper[^1])) bollReading = "数据不足。";
        else
        {
            double up = boll.Upper[^1], mid = boll.Mid[^1], lo = boll.Lower[^1];
            double width = up - lo;
            double pos = width > 0 ? (last - lo) / width : 0.5;
            string posDesc = pos > 1 ? $"已突破上轨 {up:F2}（短期偏强但易回调）"
                : pos > 0.8 ? $"接近上轨 {up:F2}（强势区）"
                : pos < 0 ? $"已跌破下轨 {lo:F2}（短期偏弱但易反弹）"
                : pos < 0.2 ? $"接近下轨 {lo:F2}（弱势区）"
                : $"在中轨 {mid:F2} 附近震荡";
            bollReading = $"通道宽度 {width:F2}，价格 {posDesc}。";
        }

        list.Add(new Item(
            "BOLL 布林带",
            "由三条线组成的\"价格通道\"——中轨是 20 日均价，上下轨是均价 ± 2 倍标准差。" +
            "价格大多数时间在通道内运行：贴近上轨偏强、贴近下轨偏弱、突破上下轨则可能反向回归。" +
            "通道变窄=波动减小（蓄势），变宽=波动放大。",
            bollReading
        ));

        // ---------- 量能 ----------
        var vols = bars.Select(b => b.Volume).ToArray();
        double v5 = vols.Skip(Math.Max(0, n - 5)).Average();
        double v20 = vols.Skip(Math.Max(0, n - 20)).Average();
        string volReading;
        bool isUp = last > prev;
        if (v20 <= 0) volReading = "数据不足。";
        else
        {
            double r = v5 / v20;
            string mag = r > 1.5 ? "明显放量" : r < 0.7 ? "明显缩量" : "量能平稳";
            string meaning;
            if (r > 1.5 && isUp) meaning = "放量上涨——有资金积极买入推升，趋势可信度高。";
            else if (r > 1.5 && !isUp) meaning = "放量下跌——有资金恐慌出逃，杀跌动能强，注意风险。";
            else if (r < 0.7 && isUp) meaning = "缩量上涨——上涨有人参与不足，谨防虚假突破。";
            else if (r < 0.7 && !isUp) meaning = "缩量下跌——抛压减轻，可能临近止跌。";
            else meaning = "量价配合一般，无显著资金动作。";
            volReading = $"近 5 日均量 / 近 20 日均量 = {r:F2}（{mag}）。{meaning}";
        }

        list.Add(new Item(
            "成交量",
            "当天有多少股被买卖。\"量能\"=资金活跃度。常说\"量在价先\"——放量配合上涨更可信，" +
            "缩量上涨往往不持久；放量下跌则要警惕，缩量下跌反而可能见底。",
            volReading
        ));

        // ---------- 筹码 ----------
        if (chip != null && chip.AvgCost > 0)
        {
            double diff = (last - chip.AvgCost) / chip.AvgCost * 100;
            string side = diff >= 0 ? $"高出 {diff:F1}%" : $"低于 {-diff:F1}%";
            string profit = chip.ProfitRatio >= 0.7 ? "市场上大部分人都赚钱了（获利盘较多，警惕兑现卖压）"
                : chip.ProfitRatio >= 0.5 ? "约一半人赚钱，多空较为均衡"
                : chip.ProfitRatio >= 0.3 ? "大部分人浮亏，反弹会遇到解套抛压"
                : "绝大多数人套牢，深度调整后反弹空间相对较大";
            string conc = chip.Concentration70 < 0.10 ? "筹码高度集中（变盘信号，方向待选）"
                : chip.Concentration70 < 0.20 ? "筹码较为集中"
                : "筹码分散（持仓成本差异大）";

            list.Add(new Item(
                "筹码分布",
                "把所有持股人的\"成本价\"画成直方图。平均成本=市场整体的持仓均价；" +
                "获利盘=当前价之下的筹码占比，比例越高代表越多人赚钱（潜在卖压也越大）；" +
                "70% 集中度=70% 的筹码集中在多大的价格区间，越小说明大家成本越接近，越可能变盘。",
                $"平均成本 {chip.AvgCost:F2}，现价{side}。获利盘 {chip.ProfitRatio:P1}——{profit}。{conc}（{chip.Concentration70:P1}）。"
            ));
        }

        return list;
    }

    public static string Format(IReadOnlyList<Item> items)
    {
        if (items.Count == 0) return "数据不足，无法生成解读。";
        var sb = new StringBuilder();
        sb.AppendLine("════════════ 指标小白解读 ════════════");
        sb.AppendLine();
        foreach (var it in items)
        {
            sb.AppendLine($"【{it.Name}】");
            sb.AppendLine($"  · 是什么：{it.WhatIsIt}");
            sb.AppendLine($"  · 当前情况：{it.CurrentReading}");
            sb.AppendLine();
        }
        return sb.ToString();
    }
}
