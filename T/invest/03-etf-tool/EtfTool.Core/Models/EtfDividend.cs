
namespace EtfTool.Core.Models
{
    public class EtfDividend
    {
        public string EtfCode { get; set; } = string.Empty;
        public DateTime? DividendDate { get; set; }
        public decimal? DividendPerUnit { get; set; }
        public string DividendType { get; set; } = string.Empty;
        public string Remark { get; set; } = string.Empty;
    }
}
