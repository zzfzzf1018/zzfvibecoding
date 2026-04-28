namespace ComparetoolWpf.Models;

/// <summary>
/// 单指标的期间对比结果（同一只股票，两期之间）。
/// </summary>
public class ComparisonRow
{
    /// <summary>指标中文名。</summary>
    public string Item { get; set; } = string.Empty;

    /// <summary>基准期数值。</summary>
    public double? BaseValue { get; set; }

    /// <summary>对比期数值。</summary>
    public double? CompareValue { get; set; }

    /// <summary>绝对变化 = CompareValue - BaseValue。</summary>
    public double? AbsoluteChange =>
        (BaseValue.HasValue && CompareValue.HasValue) ? CompareValue - BaseValue : null;

    /// <summary>
    /// 相对变化率 = (CompareValue - BaseValue) / |BaseValue|。
    /// BaseValue 为 0 或空时返回 null。
    /// </summary>
    public double? ChangeRatio
    {
        get
        {
            if (!BaseValue.HasValue || !CompareValue.HasValue) return null;
            if (Math.Abs(BaseValue.Value) < 1e-9) return null;
            return (CompareValue.Value - BaseValue.Value) / Math.Abs(BaseValue.Value);
        }
    }

    /// <summary>是否被标记为“显著变化”（由比较服务根据阈值设置）。</summary>
    public bool IsHighlighted { get; set; }
}

/// <summary>
/// 多股票横向百分比对比的一行：同一指标在多只股票上的“占基准项的比重”。
/// </summary>
public class MultiStockRow
{
    /// <summary>指标名称。</summary>
    public string Item { get; set; } = string.Empty;

    /// <summary>StockFullCode -> 原始数值。</summary>
    public Dictionary<string, double?> RawValues { get; set; } = new();

    /// <summary>StockFullCode -> 占基准项的百分比（0~1）。</summary>
    public Dictionary<string, double?> Percentages { get; set; } = new();
}
