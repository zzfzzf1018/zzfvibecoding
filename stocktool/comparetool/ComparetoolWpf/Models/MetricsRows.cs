namespace ComparetoolWpf.Models;

/// <summary>同比 / 环比 一行结果。</summary>
public class GrowthRow
{
    /// <summary>报告期标签，如 2024年报。</summary>
    public string PeriodLabel { get; set; } = string.Empty;
    /// <summary>报告期。</summary>
    public DateTime ReportDate { get; set; }
    /// <summary>本期值。</summary>
    public double? Value { get; set; }
    /// <summary>同比基期值（上年同期）。</summary>
    public double? YoYBase { get; set; }
    /// <summary>同比增速。</summary>
    public double? YoY { get; set; }
    /// <summary>环比基期值（上一报告期）。</summary>
    public double? QoQBase { get; set; }
    /// <summary>环比增速。</summary>
    public double? QoQ { get; set; }
}

/// <summary>杜邦分析（ROE = 净利率 × 总资产周转率 × 权益乘数）。</summary>
public class DuPontRow
{
    public string PeriodLabel { get; set; } = string.Empty;
    public DateTime ReportDate { get; set; }
    /// <summary>归母净利润。</summary>
    public double? NetProfit { get; set; }
    /// <summary>营业总收入。</summary>
    public double? Revenue { get; set; }
    /// <summary>资产总计（期末）。</summary>
    public double? TotalAssets { get; set; }
    /// <summary>归母股东权益（期末）。</summary>
    public double? Equity { get; set; }
    /// <summary>销售净利率 = 净利润 / 营收。</summary>
    public double? NetMargin { get; set; }
    /// <summary>资产周转率 = 营收 / 总资产。</summary>
    public double? AssetTurnover { get; set; }
    /// <summary>权益乘数 = 总资产 / 股东权益。</summary>
    public double? EquityMultiplier { get; set; }
    /// <summary>ROE = 净利率 × 周转率 × 权益乘数。</summary>
    public double? Roe { get; set; }
}

/// <summary>
/// 营运能力 / 现金流质量 一行：
/// 包含营运资本变化、自由现金流（FCF）、现金转换周期（CCC）等。
/// </summary>
public class CashQualityRow
{
    public string PeriodLabel { get; set; } = string.Empty;
    public DateTime ReportDate { get; set; }

    /// <summary>营运资本 = 流动资产合计 - 流动负债合计。</summary>
    public double? WorkingCapital { get; set; }
    /// <summary>营运资本变化（与上一相邻报告期相比）。</summary>
    public double? WorkingCapitalChange { get; set; }

    /// <summary>经营活动现金流量净额 (OCF)。</summary>
    public double? OperatingCashFlow { get; set; }
    /// <summary>资本开支 (Capex)：投资活动现金流出小计的近似。</summary>
    public double? Capex { get; set; }
    /// <summary>自由现金流 = OCF - |Capex|。</summary>
    public double? FreeCashFlow { get; set; }

    /// <summary>应收账款周转天数 DSO = 应收 / 营收 × 365。</summary>
    public double? DSO { get; set; }
    /// <summary>存货周转天数 DIO = 存货 / 营业成本 × 365。</summary>
    public double? DIO { get; set; }
    /// <summary>应付账款周转天数 DPO = 应付 / 营业成本 × 365。</summary>
    public double? DPO { get; set; }
    /// <summary>现金转换周期 CCC = DSO + DIO - DPO。</summary>
    public double? CCC { get; set; }
}
