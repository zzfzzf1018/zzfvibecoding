
namespace EtfTool.Core.Models
{
    public class EtfComponent
    {
        public string EtfCode { get; set; } = string.Empty;
        public string StockCode { get; set; } = string.Empty;
        public string StockName { get; set; } = string.Empty;
        public decimal? Weight { get; set; }
        public decimal? Price { get; set; }
        public decimal? ChangePercent { get; set; }
        public decimal? PeRatio { get; set; }
        public decimal? PbRatio { get; set; }
        public int? Rank { get; set; }
        public DateTime? UpdateTime { get; set; }
    }
}
