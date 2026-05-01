using System.Collections.Generic;

namespace PdfToMarkdown.Models
{
    public enum BlockType
    {
        Title,
        Heading,
        Paragraph,
        Table,
        List,
        PageBreak
    }

    public class PdfContentBlock
    {
        public BlockType Type { get; set; }
        public string Text { get; set; } = string.Empty;
        public int PageNumber { get; set; }
        public double FontSize { get; set; }
        public bool IsBold { get; set; }
        public List<List<string>>? TableData { get; set; }
    }

    public class FinancialData
    {
        public string CompanyName { get; set; } = string.Empty;
        public string StockCode { get; set; } = string.Empty;
        public string ReportYear { get; set; } = string.Empty;
        public List<QuarterlyFinancial> QuarterlyData { get; set; } = new();
        public Dictionary<string, string> KeyMetrics { get; set; } = new();
    }

    public class QuarterlyFinancial
    {
        public string Period { get; set; } = string.Empty;
        public decimal Revenue { get; set; }
        public decimal NetProfit { get; set; }
        public decimal GrossProfit { get; set; }
        public decimal TotalAssets { get; set; }
        public decimal TotalLiabilities { get; set; }
        public decimal NetAssets { get; set; }
        public decimal EarningsPerShare { get; set; }
        public decimal OperatingCashFlow { get; set; }
        public string RawText { get; set; } = string.Empty;
    }
}
