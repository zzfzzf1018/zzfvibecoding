
namespace EtfTool.Core.Models
{
    public class EtfStatistics
    {
        public string EtfCode { get; set; } = string.Empty;
        public decimal? CurrentPe { get; set; }
        public decimal? CurrentPb { get; set; }
        public decimal? AvgPe { get; set; }
        public decimal? AvgPb { get; set; }
        public decimal? PeMin { get; set; }
        public decimal? PeMax { get; set; }
        public decimal? PbMin { get; set; }
        public decimal? PbMax { get; set; }
        public decimal? PePercentile { get; set; }
        public decimal? PbPercentile { get; set; }
        public int? DataCount { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
    }
}
