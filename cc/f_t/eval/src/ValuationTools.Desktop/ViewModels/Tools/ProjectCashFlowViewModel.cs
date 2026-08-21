using System.Data;
using ValuationTools.Core.Calculators;
using ValuationTools.Desktop.Models;

namespace ValuationTools.Desktop.ViewModels.Tools;

public sealed class ProjectCashFlowViewModel : ToolViewModel
{
    private const int MaxYears = 10;

    public ProjectCashFlowViewModel()
        : base("NPV / IRR 项目评估",
               "现金流工具",
               "对任意现金流序列计算净现值、内部收益率、修正内部收益率、获利指数与回收期。")
    {
        Formula = "NPV = −C0 + Σ CFt / (1+r)^t；IRR 为使 NPV = 0 的折现率";

        AddGroup("投资与折现率",
            Number("initial", "初始投资（t=0 流出）", 10000, " 万元"),
            Percent("rate", "折现率 / 资金成本", 10),
            Percent("reinvest", "再投资收益率", 8, "用于 MIRR，填 0 则采用折现率"),
            Number("years", "现金流年数", 6, " 年", $"最多 {MaxYears} 年"));

        var flows = new List<InputField>();
        double[] defaults = { 2000, 2500, 3000, 3200, 3500, 3800, 0, 0, 0, 0 };
        for (int i = 1; i <= MaxYears; i++)
            flows.Add(Number($"cf{i}", $"第 {i} 年现金流", defaults[i - 1], " 万元"));
        AddGroup("各年净现金流", flows.ToArray());

        Ready();
    }

    protected override void Compute()
    {
        int years = Math.Clamp(I("years"), 1, MaxYears);
        var flows = new List<double>();
        for (int i = 1; i <= years; i++)
            flows.Add(V($"cf{i}"));

        var result = ProjectCashFlowCalculator.Calculate(new ProjectCashFlowInput
        {
            InitialInvestment = V("initial"),
            CashFlows = flows,
            DiscountRate = R("rate"),
            ReinvestmentRate = R("reinvest")
        });

        AddResult("净现值 NPV", Money0(result.Npv) + " 万元", isPrimary: true, note: result.Judgement);
        AddResult("内部收益率 IRR", result.Irr.HasValue ? Pct(result.Irr.Value) : "无解");
        AddResult("修正内部收益率 MIRR", result.Mirr.HasValue ? Pct(result.Mirr.Value) : "无解");
        AddResult("获利指数 PI", Num(result.ProfitabilityIndex, 3), note: "大于 1 表示项目可行");
        AddResult("未来现金流现值合计", Money0(result.TotalPresentValue) + " 万元");
        AddResult("静态回收期", result.PaybackPeriod.HasValue ? Num(result.PaybackPeriod.Value, 2) + " 年" : "无法回收");
        AddResult("折现回收期", result.DiscountedPaybackPeriod.HasValue ? Num(result.DiscountedPaybackPeriod.Value, 2) + " 年" : "无法回收");

        var table = CreateTable("年份", "现金流", "折现因子", "现值", "累计现值");
        table.Rows.Add("第 0 年", Money0(-Math.Abs(V("initial"))), "1.0000", Money0(-Math.Abs(V("initial"))), Money0(-Math.Abs(V("initial"))));
        foreach (var row in result.Years)
            table.Rows.Add($"第 {row.Year} 年", Money0(row.CashFlow), Num(row.DiscountFactor, 4), Money0(row.PresentValue),
                Money0(row.CumulativePresentValue - Math.Abs(V("initial"))));
        SetSchedule(table);

        SetSensitivity(BuildSensitivity(flows));
    }

    private DataTable BuildSensitivity(List<double> flows)
    {
        double baseRate = R("rate");
        double[] rateOffsets = { -0.04, -0.02, 0, 0.02, 0.04 };
        double[] flowScales = { 0.8, 0.9, 1.0, 1.1, 1.2 };

        var headers = new List<string> { "现金流调整 \\ 折现率" };
        headers.AddRange(rateOffsets.Select(offset => Pct(baseRate + offset)));
        var table = CreateTable(headers.ToArray());

        foreach (var scale in flowScales)
        {
            var row = table.NewRow();
            row[0] = Pct(scale - 1);
            for (int i = 0; i < rateOffsets.Length; i++)
            {
                try
                {
                    var npv = ProjectCashFlowCalculator.Calculate(new ProjectCashFlowInput
                    {
                        InitialInvestment = V("initial"),
                        CashFlows = flows.Select(f => f * scale).ToList(),
                        DiscountRate = baseRate + rateOffsets[i],
                        ReinvestmentRate = R("reinvest")
                    }).Npv;
                    row[i + 1] = Money0(npv);
                }
                catch
                {
                    row[i + 1] = "—";
                }
            }
            table.Rows.Add(row);
        }

        SensitivityTitle = "NPV 敏感性分析（万元）";
        return table;
    }
}
