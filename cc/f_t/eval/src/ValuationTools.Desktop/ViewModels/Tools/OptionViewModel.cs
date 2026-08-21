using System.Data;
using ValuationTools.Core.Calculators;

namespace ValuationTools.Desktop.ViewModels.Tools;

public sealed class OptionViewModel : ToolViewModel
{
    public OptionViewModel()
        : base("期权 / 实物期权定价",
               "现金流工具",
               "Black-Scholes-Merton 模型，可用于股票期权、可转债期权价值以及项目的实物期权估值。")
    {
        Formula = "C = S·e^(−qT)·N(d1) − K·e^(−rT)·N(d2)";

        AddGroup("标的与合约",
            Number("spot", "标的现价 S", 100, " 元", "实物期权中为项目现值"),
            Number("strike", "行权价 K", 100, " 元", "实物期权中为投资成本"),
            Number("time", "到期时间", 1, " 年"));

        AddGroup("利率与波动率",
            Percent("rf", "无风险利率", 2.5),
            Percent("vol", "年化波动率", 30),
            Percent("dividend", "股息率 / 便利收益", 0));

        Ready();
    }

    protected override void Compute()
    {
        var result = OptionCalculator.Calculate(new OptionInput
        {
            SpotPrice = V("spot"),
            StrikePrice = V("strike"),
            TimeToMaturity = V("time"),
            RiskFreeRate = R("rf"),
            Volatility = R("vol"),
            DividendYield = R("dividend")
        });

        AddResult("看涨期权价值 Call", Money(result.CallPrice) + " 元", isPrimary: true);
        AddResult("看跌期权价值 Put", Money(result.PutPrice) + " 元");
        AddResult("内在价值", Money(Math.Max(V("spot") - V("strike"), 0)) + " 元");
        AddResult("时间价值", Money(result.CallPrice - Math.Max(V("spot") - V("strike"), 0)) + " 元");
        AddResult("行权概率 N(d2)", Pct(result.ExerciseProbability));
        AddResult("Delta（看涨 / 看跌）", $"{Num(result.CallDelta, 4)} / {Num(result.PutDelta, 4)}");
        AddResult("Gamma", Num(result.Gamma, 5));
        AddResult("Vega（波动率 +1%）", Money(result.Vega) + " 元");
        AddResult("Theta（每日）", Money(result.CallTheta) + " 元");
        AddResult("Rho（利率 +1%）", Money(result.CallRho) + " 元");

        SetSensitivity(BuildSensitivity());
    }

    private DataTable BuildSensitivity()
    {
        double baseSpot = V("spot");
        double baseVol = R("vol");
        double[] spotScales = { 0.8, 0.9, 1.0, 1.1, 1.2 };
        double[] volOffsets = { -0.10, -0.05, 0, 0.05, 0.10 };

        var headers = new List<string> { "波动率 \\ 标的价格" };
        headers.AddRange(spotScales.Select(scale => Money(baseSpot * scale)));
        var table = CreateTable(headers.ToArray());

        foreach (var volOffset in volOffsets)
        {
            var row = table.NewRow();
            double vol = baseVol + volOffset;
            row[0] = Pct(vol);
            for (int i = 0; i < spotScales.Length; i++)
            {
                try
                {
                    var value = OptionCalculator.Calculate(new OptionInput
                    {
                        SpotPrice = baseSpot * spotScales[i],
                        StrikePrice = V("strike"),
                        TimeToMaturity = V("time"),
                        RiskFreeRate = R("rf"),
                        Volatility = vol,
                        DividendYield = R("dividend")
                    }).CallPrice;
                    row[i + 1] = Money(value);
                }
                catch
                {
                    row[i + 1] = "—";
                }
            }
            table.Rows.Add(row);
        }

        SensitivityTitle = "看涨期权价值敏感性（元）";
        return table;
    }
}
