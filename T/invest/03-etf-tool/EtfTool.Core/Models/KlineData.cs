
namespace EtfTool.Core.Models
{
    public class KlineData
    {
        public string EtfCode { get; set; } = string.Empty;
        public DateTime Date { get; set; }
        public decimal Open { get; set; }
        public decimal High { get; set; }
        public decimal Low { get; set; }
        public decimal Close { get; set; }
        public decimal Volume { get; set; }
        public decimal Amount { get; set; }
        public decimal? PeRatio { get; set; }
        public decimal? PbRatio { get; set; }
        public decimal? ChangePercent { get; set; }
    }
}
