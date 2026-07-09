
namespace EtfTool.Core.Models
{
    public class EtfInfo
    {
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string Exchange { get; set; } = string.Empty;
        public decimal? TotalAssets { get; set; }
        public decimal? Unit { get; set; }
        public decimal? LatestPrice { get; set; }
        public decimal? ChangePercent { get; set; }
        public decimal? PeRatio { get; set; }
        public decimal? PbRatio { get; set; }
        public decimal? DividendYield { get; set; }
        public decimal? ManagementFee { get; set; }
        public decimal? CustodyFee { get; set; }
        public decimal? SalesServiceFee { get; set; }
        public decimal? SubscriptionFee { get; set; }
        public decimal? RedemptionFee { get; set; }
        public DateTime? ListedDate { get; set; }
        public DateTime? UpdateTime { get; set; }
    }
}
