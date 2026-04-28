using System.Collections;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using ClosedXML.Excel;

namespace ComparetoolWpf.Services;

/// <summary>
/// 通用集合导出工具：根据对象的公有属性反射，生成 CSV 或 XLSX。
/// </summary>
public static class ExportService
{
    /// <summary>导出对象集合为 CSV（UTF-8 with BOM，Excel 打开不乱码）。</summary>
    public static void ExportCsv<T>(IEnumerable<T> rows, string path)
    {
        var props = typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance);
        var sb = new StringBuilder();
        sb.AppendLine(string.Join(",", props.Select(p => Escape(p.Name))));
        foreach (var r in rows)
        {
            sb.AppendLine(string.Join(",", props.Select(p => Escape(Format(p.GetValue(r))))));
        }
        File.WriteAllText(path, sb.ToString(), new UTF8Encoding(true));
    }

    /// <summary>导出为 XLSX。</summary>
    public static void ExportExcel<T>(IEnumerable<T> rows, string path, string sheetName = "Sheet1")
    {
        var props = typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance);
        using var wb = new XLWorkbook();
        var ws = wb.AddWorksheet(SafeSheetName(sheetName));
        for (int c = 0; c < props.Length; c++)
            ws.Cell(1, c + 1).Value = props[c].Name;
        int rowIdx = 2;
        foreach (var r in rows)
        {
            for (int c = 0; c < props.Length; c++)
            {
                var v = props[c].GetValue(r);
                ws.Cell(rowIdx, c + 1).Value = ToCellValue(v);
            }
            rowIdx++;
        }
        ws.Columns().AdjustToContents();
        wb.SaveAs(path);
    }

    /// <summary>把任意 IEnumerable 写到 Excel 的多个 sheet 中。</summary>
    public static void ExportExcelMultiSheet(IDictionary<string, IEnumerable> sheets, string path)
    {
        using var wb = new XLWorkbook();
        foreach (var (name, rows) in sheets)
        {
            var ws = wb.AddWorksheet(SafeSheetName(name));
            WriteSheet(ws, rows);
            ws.Columns().AdjustToContents();
        }
        wb.SaveAs(path);
    }

    private static void WriteSheet(IXLWorksheet ws, IEnumerable rows)
    {
        Type? itemType = null;
        var list = rows.Cast<object>().ToList();
        if (list.Count > 0) itemType = list[0]!.GetType();
        if (itemType == null)
        {
            ws.Cell(1, 1).Value = "(空)";
            return;
        }
        var props = itemType.GetProperties(BindingFlags.Public | BindingFlags.Instance);
        for (int c = 0; c < props.Length; c++)
            ws.Cell(1, c + 1).Value = props[c].Name;
        int rowIdx = 2;
        foreach (var r in list)
        {
            for (int c = 0; c < props.Length; c++)
            {
                var v = props[c].GetValue(r);
                ws.Cell(rowIdx, c + 1).Value = ToCellValue(v);
            }
            rowIdx++;
        }
    }

    private static XLCellValue ToCellValue(object? v)
    {
        if (v == null) return Blank.Value;
        return v switch
        {
            string s => s,
            bool b => b,
            DateTime d => d,
            double dv => dv,
            float fv => fv,
            int iv => iv,
            long lv => lv,
            decimal mv => mv,
            _ => v.ToString() ?? string.Empty,
        };
    }

    private static string Escape(string s)
    {
        if (s.Contains(',') || s.Contains('"') || s.Contains('\n'))
            return "\"" + s.Replace("\"", "\"\"") + "\"";
        return s;
    }

    private static string Format(object? v)
    {
        if (v == null) return string.Empty;
        return v switch
        {
            double d => d.ToString("R", CultureInfo.InvariantCulture),
            DateTime dt => dt.ToString("yyyy-MM-dd"),
            _ => v.ToString() ?? string.Empty,
        };
    }

    private static string SafeSheetName(string s)
    {
        var name = string.Concat(s.Where(c => !"\\/?*[]:".Contains(c)));
        return string.IsNullOrEmpty(name) ? "Sheet" : (name.Length > 31 ? name.Substring(0, 31) : name);
    }
}
