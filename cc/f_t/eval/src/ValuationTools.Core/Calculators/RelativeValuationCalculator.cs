namespace ValuationTools.Core.Calculators;

public sealed class RelativeValuationInput
{
    public double Price { get; init; }
    public double SharesOutstanding { get; init; }
    public double NetDebt { get; init; }

    public double EarningsPerShare { get; init; }
    public double BookValuePerShare { get; init; }
    public double SalesPerShare { get; init; }
    public double CashFlowPerShare { get; init; }
    public double Ebitda { get; init; }

    public double PeerPe { get; init; }
    public double PeerPb { get; init; }
    public double PeerPs { get; init; }
    public double PeerPcf { get; init; }
    public double PeerEvToEbitda { get; init; }
}

public sealed record MultipleRow(
    string Method,
    double CurrentMultiple,
    double PeerMultiple,
    double ImpliedPrice,
    double UpsidePercent,
    bool Applicable);

public sealed class RelativeValuationResult
{
    public IReadOnlyList<MultipleRow> Rows { get; init; } = Array.Empty<MultipleRow>();
    public double MarketCap { get; init; }
    public double EnterpriseValue { get; init; }
    public double AverageImpliedPrice { get; init; }
    public double MedianImpliedPrice { get; init; }
    public double? UpsidePercent { get; init; }
    public string? Warning { get; init; }
}

/// <summary>相对估值（可比公司倍数法）：PE / PB / PS / P·CF / EV·EBITDA。</summary>
public static class RelativeValuationCalculator
{
    public static RelativeValuationResult Calculate(RelativeValuationInput input)
    {
        double marketCap = input.Price * input.SharesOutstanding;
        double enterpriseValue = marketCap + input.NetDebt;
        var rows = new List<MultipleRow>();

        rows.Add(PerShareRow("市盈率 PE", input.Price, input.EarningsPerShare, input.PeerPe));
        rows.Add(PerShareRow("市净率 PB", input.Price, input.BookValuePerShare, input.PeerPb));
        rows.Add(PerShareRow("市销率 PS", input.Price, input.SalesPerShare, input.PeerPs));
        rows.Add(PerShareRow("市现率 P/CF", input.Price, input.CashFlowPerShare, input.PeerPcf));

        // EV/EBITDA：先得到企业价值，再扣净债务折算到每股
        bool evApplicable = input.Ebitda > 0 && input.PeerEvToEbitda > 0 && input.SharesOutstanding > 0;
        double currentEvMultiple = input.Ebitda > 0 ? enterpriseValue / input.Ebitda : 0;
        double impliedEvPrice = evApplicable
            ? (input.PeerEvToEbitda * input.Ebitda - input.NetDebt) / input.SharesOutstanding
            : 0;
        rows.Add(new MultipleRow(
            "EV/EBITDA",
            currentEvMultiple,
            input.PeerEvToEbitda,
            impliedEvPrice,
            input.Price > 0 && evApplicable ? impliedEvPrice / input.Price - 1.0 : 0,
            evApplicable));

        var valid = rows.Where(r => r.Applicable && r.ImpliedPrice > 0).Select(r => r.ImpliedPrice).OrderBy(v => v).ToList();
        double average = valid.Count > 0 ? valid.Average() : 0;
        double median = valid.Count == 0
            ? 0
            : valid.Count % 2 == 1
                ? valid[valid.Count / 2]
                : (valid[valid.Count / 2 - 1] + valid[valid.Count / 2]) / 2.0;

        string? warning = valid.Count == 0
            ? "没有可用的倍数，请至少填写一组「公司指标 + 可比公司倍数」。"
            : valid.Count > 1 && valid[^1] / valid[0] > 3
                ? "各方法估值结果差异超过 3 倍，说明可比公司选择或指标口径可能不一致。"
                : null;

        return new RelativeValuationResult
        {
            Rows = rows,
            MarketCap = marketCap,
            EnterpriseValue = enterpriseValue,
            AverageImpliedPrice = average,
            MedianImpliedPrice = median,
            UpsidePercent = input.Price > 0 && average > 0 ? average / input.Price - 1.0 : null,
            Warning = warning
        };
    }

    private static MultipleRow PerShareRow(string method, double price, double perShareMetric, double peerMultiple)
    {
        bool applicable = perShareMetric > 0 && peerMultiple > 0;
        double current = perShareMetric > 0 && price > 0 ? price / perShareMetric : 0;
        double implied = applicable ? peerMultiple * perShareMetric : 0;
        double upside = applicable && price > 0 ? implied / price - 1.0 : 0;
        return new MultipleRow(method, current, peerMultiple, implied, upside, applicable);
    }
}
