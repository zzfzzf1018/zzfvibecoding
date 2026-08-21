using System.Data;
using ValuationTools.Core.Calculators;

namespace ValuationTools.Desktop.ViewModels.Tools;

public sealed class RelativeValuationViewModel : ToolViewModel
{
    public RelativeValuationViewModel()
        : base("可比公司倍数法",
               "相对估值",
               "用同行业可比公司的 PE / PB / PS / P·CF / EV·EBITDA 倍数，反推目标公司的合理股价。")
    {
        Formula = "隐含股价 = 可比倍数 × 对应每股指标；EV/EBITDA 法需再扣除净债务";

        AddGroup("公司基本数据",
            Number("price", "当前股价", 30, " 元"),
            Number("shares", "总股本", 10000, " 万股"),
            Number("netDebt", "净债务", 20000, " 万元"));

        AddGroup("每股指标",
            Number("eps", "每股收益 EPS", 1.5, " 元"),
            Number("bvps", "每股净资产 BVPS", 8, " 元"),
            Number("sps", "每股营业收入", 12, " 元"),
            Number("cfps", "每股经营现金流", 2.2, " 元"),
            Number("ebitda", "EBITDA 总额", 25000, " 万元"));

        AddGroup("可比公司倍数",
            Number("peerPe", "行业 PE", 22, "x"),
            Number("peerPb", "行业 PB", 3.5, "x"),
            Number("peerPs", "行业 PS", 2.6, "x"),
            Number("peerPcf", "行业 P/CF", 15, "x"),
            Number("peerEv", "行业 EV/EBITDA", 12, "x"));

        Ready();
    }

    protected override void Compute()
    {
        var result = RelativeValuationCalculator.Calculate(new RelativeValuationInput
        {
            Price = V("price"),
            SharesOutstanding = V("shares"),
            NetDebt = V("netDebt"),
            EarningsPerShare = V("eps"),
            BookValuePerShare = V("bvps"),
            SalesPerShare = V("sps"),
            CashFlowPerShare = V("cfps"),
            Ebitda = V("ebitda"),
            PeerPe = V("peerPe"),
            PeerPb = V("peerPb"),
            PeerPs = V("peerPs"),
            PeerPcf = V("peerPcf"),
            PeerEvToEbitda = V("peerEv")
        });

        AddResult("各方法平均隐含股价", Money(result.AverageImpliedPrice) + " 元", isPrimary: true);
        AddResult("中位数隐含股价", Money(result.MedianImpliedPrice) + " 元");
        AddResult("相对当前股价", Pct(result.UpsidePercent));
        AddResult("当前总市值", Money0(result.MarketCap) + " 万元");
        AddResult("当前企业价值 EV", Money0(result.EnterpriseValue) + " 万元");

        var table = CreateTable("估值方法", "公司当前倍数", "可比公司倍数", "隐含股价", "相对空间");
        foreach (var row in result.Rows)
        {
            table.Rows.Add(
                row.Method,
                row.CurrentMultiple > 0 ? Times(row.CurrentMultiple) : "—",
                row.PeerMultiple > 0 ? Times(row.PeerMultiple) : "—",
                row.Applicable ? Money(row.ImpliedPrice) + " 元" : "数据不足",
                row.Applicable ? Pct(row.UpsidePercent) : "—");
        }

        ScheduleTitle = "各倍数法估值对比";
        SetSchedule(table);
        SetNotice(result.Warning);
    }
}
