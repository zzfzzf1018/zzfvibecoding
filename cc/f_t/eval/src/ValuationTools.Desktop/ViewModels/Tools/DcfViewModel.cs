using System.Data;
using ValuationTools.Core.Calculators;

namespace ValuationTools.Desktop.ViewModels.Tools;

public sealed class DcfViewModel : ToolViewModel
{
    public DcfViewModel()
        : base("DCF 现金流折现",
               "绝对估值",
               "多阶段自由现金流折现，支持永续增长法与退出倍数法终值、期中折现及双因素敏感性分析。")
    {
        Formula = "企业价值 = Σ FCFt / (1+WACC)^t + 终值现值；股权价值 = 企业价值 − 净债务";

        AddGroup("现金流预测",
            Number("fcf0", "基期自由现金流", 10000, " 万元", "FCFF 用 WACC 折现；FCFE 用股权成本折现且净债务填 0"),
            Number("y1", "第一阶段年数", 5, " 年"),
            Percent("g1", "第一阶段增长率", 12),
            Number("y2", "第二阶段年数", 5, " 年"),
            Percent("g2", "第二阶段增长率", 6));

        AddGroup("折现率与终值",
            Percent("wacc", "折现率 WACC", 9, "可在「折现率 WACC/CAPM」工具中测算"),
            Choice("method", "终值方法", new[] { "永续增长法（戈登）", "退出倍数法" }),
            Percent("tg", "永续增长率", 2.5, "长期不应高于名义 GDP 增速"),
            Number("exitMultiple", "退出倍数", 8, "x", "仅退出倍数法使用"),
            Number("terminalMetric", "终期指标（如末年 EBITDA）", 0, " 万元", "留空或 0 则使用末年自由现金流"),
            Toggle("midYear", "采用期中折现法", false, "假设现金流均匀发生在年内"));

        AddGroup("股权价值换算",
            Number("netDebt", "净债务（有息负债 − 现金）", 20000, " 万元"),
            Number("shares", "总股本", 10000, " 万股"),
            Number("price", "当前股价", 12, " 元"),
            Percent("mos", "安全边际", 30));

        Ready();
    }

    protected override void Compute()
    {
        var input = BuildInput(R("wacc"), R("tg"), V("price"));
        var result = DcfCalculator.Calculate(input);

        AddResult("每股内在价值", Money(result.ValuePerShare) + " 元", isPrimary: true);
        AddResult("建议买入价（含安全边际）", Money(result.BuyBelowPrice) + " 元");
        AddResult("相对当前股价", Pct(result.UpsidePercent), note: result.UpsidePercent is > 0 ? "低估" : result.UpsidePercent is null ? null : "高估");
        AddResult("企业价值 EV", Money0(result.EnterpriseValue) + " 万元");
        AddResult("股权价值", Money0(result.EquityValue) + " 万元");
        AddResult("预测期现金流现值", Money0(result.PresentValueOfForecast) + " 万元");
        AddResult("终值", Money0(result.TerminalValue) + " 万元");
        AddResult("终值现值", Money0(result.PresentValueOfTerminal) + " 万元", note: $"占 EV 的 {Pct(result.TerminalWeight)}");
        AddResult("当前股价隐含永续增长率", Pct(result.ImpliedTerminalGrowth));

        SetSchedule(BuildSchedule(result));
        SetSensitivity(BuildSensitivity());
        SetNotice(result.Warning);
    }

    private DcfInput BuildInput(double discountRate, double terminalGrowth, double currentPrice) => new()
    {
        BaseCashFlow = V("fcf0"),
        Stage1Years = I("y1"),
        Stage1Growth = R("g1"),
        Stage2Years = I("y2"),
        Stage2Growth = R("g2"),
        DiscountRate = discountRate,
        TerminalGrowth = terminalGrowth,
        TerminalMethod = Selected("method") == 0 ? TerminalValueMethod.GordonGrowth : TerminalValueMethod.ExitMultiple,
        ExitMultiple = V("exitMultiple"),
        TerminalMetric = V("terminalMetric"),
        MidYearConvention = B("midYear"),
        NetDebt = V("netDebt"),
        SharesOutstanding = V("shares"),
        CurrentPrice = currentPrice,
        MarginOfSafety = R("mos")
    };

    private static DataTable BuildSchedule(DcfResult result)
    {
        var table = CreateTable("年份", "增长率", "自由现金流", "折现因子", "现值");

        foreach (var row in result.Years)
            table.Rows.Add($"第 {row.Year} 年", Pct(row.GrowthRate), Money0(row.CashFlow), Num(row.DiscountFactor, 4), Money0(row.PresentValue));

        table.Rows.Add("终值", "—", Money0(result.TerminalValue), Num(result.PresentValueOfTerminal / (result.TerminalValue == 0 ? 1 : result.TerminalValue), 4), Money0(result.PresentValueOfTerminal));
        table.Rows.Add("合计（企业价值）", "—", "—", "—", Money0(result.EnterpriseValue));
        return table;
    }

    private DataTable BuildSensitivity()
    {
        double baseWacc = R("wacc");
        double baseGrowth = R("tg");
        double[] waccOffsets = { -0.01, -0.005, 0, 0.005, 0.01 };
        double[] growthOffsets = { -0.01, -0.005, 0, 0.005, 0.01 };

        var headers = new List<string> { "永续增长率 \\ 折现率" };
        headers.AddRange(waccOffsets.Select(offset => Pct(baseWacc + offset)));
        var table = CreateTable(headers.ToArray());

        foreach (var gOffset in growthOffsets)
        {
            double growth = baseGrowth + gOffset;
            var row = table.NewRow();
            row[0] = Pct(growth);
            for (int i = 0; i < waccOffsets.Length; i++)
            {
                double wacc = baseWacc + waccOffsets[i];
                try
                {
                    var value = DcfCalculator.Calculate(BuildInput(wacc, growth, 0)).ValuePerShare;
                    row[i + 1] = Money(value);
                }
                catch
                {
                    row[i + 1] = "—";
                }
            }
            table.Rows.Add(row);
        }
        return table;
    }
}
