using System.Collections;
using System.IO;
using System.Reflection;
using ClosedXML.Excel;
using ComparetoolWpf.Services;
using Xunit;

namespace ComparetoolWpf.Tests;

public class ExportServiceTests
{
    private class Row
    {
        public string Name { get; set; } = "";
        public double? Value { get; set; }
        public DateTime When { get; set; }
        public bool IsHighlighted { get; set; }
    }

    [Fact]
    public void ExportCsv_WritesHeadersAndRows_EscapesSpecialChars()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".csv");
        try
        {
            ExportService.ExportCsv(new[]
            {
                new Row { Name = "a,b", Value = 1.5, When = new DateTime(2024,1,2) },
                new Row { Name = "x\"y", Value = null, When = default },
            }, path);
            var text = File.ReadAllText(path);
            Assert.Contains("Name,Value,When,IsHighlighted", text);
            Assert.Contains("\"a,b\"", text);
            Assert.Contains("\"x\"\"y\"", text);
            Assert.Contains("2024-01-02", text);
            Assert.Contains("1.5", text);
        }
        finally { if (File.Exists(path)) File.Delete(path); }
    }

    [Fact]
    public void ExportExcel_HighlightsRow_AndSkipsHighlightColumn()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".xlsx");
        try
        {
            ExportService.ExportExcel(new[]
            {
                new Row { Name = "a", Value = 1, IsHighlighted = false },
                new Row { Name = "b", Value = 2, IsHighlighted = true },
            }, path, "data?/sheet:long-name-that-is-way-over-the-limit-of-31-chars");

            using var wb = new XLWorkbook(path);
            var ws = wb.Worksheet(1);
            // 表头不应包含 IsHighlighted 列
            var headers = new[] { ws.Cell(1, 1).GetString(), ws.Cell(1, 2).GetString(), ws.Cell(1, 3).GetString() };
            Assert.DoesNotContain("IsHighlighted", headers);
            Assert.True(ws.Name.Length <= 31);
            // 第二行（索引 3 即 row b）背景色应非空（FFE0E0）
            var fill = ws.Cell(3, 1).Style.Fill.BackgroundColor;
            Assert.NotEqual(XLColor.NoColor, fill);
        }
        finally { if (File.Exists(path)) File.Delete(path); }
    }

    [Fact]
    public void ExportExcel_NoHighlightProperty_StillWorks()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".xlsx");
        try
        {
            ExportService.ExportExcel(new[] { new { A = 1, B = "x" } }, path);
            Assert.True(File.Exists(path));
        }
        finally { if (File.Exists(path)) File.Delete(path); }
    }

    [Fact]
    public void ExportExcelMultiSheet_EmptyAndPopulated()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".xlsx");
        try
        {
            var sheets = new Dictionary<string, IEnumerable>
            {
                ["empty"] = new List<Row>(),
                ["data"] = new List<Row> { new() { Name = "a", Value = 1, When = new DateTime(2024, 1, 1) } },
            };
            ExportService.ExportExcelMultiSheet(sheets, path);
            using var wb = new XLWorkbook(path);
            Assert.Equal(2, wb.Worksheets.Count);
            Assert.Equal("(空)", wb.Worksheet("empty").Cell(1, 1).GetString());
        }
        finally { if (File.Exists(path)) File.Delete(path); }
    }

    [Fact]
    public void SafeSheetName_StripsAndTruncates()
    {
        var m = typeof(ExportService).GetMethod("SafeSheetName", BindingFlags.NonPublic | BindingFlags.Static)!;
        Assert.Equal("abc", m.Invoke(null, new object[] { "a/b\\c" }));
        Assert.Equal("Sheet", m.Invoke(null, new object[] { "////" }));
        var s = (string)m.Invoke(null, new object[] { new string('x', 50) })!;
        Assert.Equal(31, s.Length);
    }
}
