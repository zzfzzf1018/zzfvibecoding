using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using PdfToMarkdown.Models;
using PdfToMarkdown.Services;

namespace PdfToMarkdown;

public class TestRunner
{
    public static void RunTest(string pdfPath)
    {
        var parser = new PdfParserService();
        var converter = new MarkdownConverterService();
        var extractor = new FinancialDataExtractor();

        var sb = new StringBuilder();
        sb.AppendLine($"解析文件: {pdfPath}");
        sb.AppendLine("========================================");

        var blocks = parser.ParsePdf(pdfPath);
        sb.AppendLine($"总块数: {blocks.Count}");

        // Find the quarterly section
        bool found = false;
        for (int i = 0; i < blocks.Count; i++)
        {
            var block = blocks[i];
            if (block.Text.Contains("分季度主要财务数据") || block.Text.Contains("分季度"))
            {
                found = true;
                sb.AppendLine($"\n====== 找到分季度区域 (块#{i}, 页{block.PageNumber}) ======");
                sb.AppendLine($"类型: {block.Type}");
                sb.AppendLine($"字体大小: {block.FontSize:F1}");
                sb.AppendLine($"文本内容:\n{block.Text}");
                sb.AppendLine();

                // Print next 8 blocks
                for (int j = i + 1; j < Math.Min(i + 10, blocks.Count); j++)
                {
                    var next = blocks[j];
                    if (next.Type == BlockType.PageBreak) continue;
                    sb.AppendLine($"  -- 块#{j} 类型:{next.Type} 页:{next.PageNumber} 字体:{next.FontSize:F1}");
                    sb.AppendLine($"  文本: {next.Text.Substring(0, Math.Min(500, next.Text.Length))}");
                    if (next.TableData != null)
                    {
                        sb.AppendLine($"  表格行数: {next.TableData.Count}");
                        foreach (var row in next.TableData.Take(8))
                        {
                            sb.AppendLine($"    | {string.Join(" | ", row)} |");
                        }
                    }
                    sb.AppendLine();
                }
                break;
            }
        }

        if (!found)
            sb.AppendLine("未找到分季度数据区域");

        // Also test financial extraction
        sb.AppendLine("\n====== 财务数据提取结果 ======");
        var financialData = extractor.ExtractFinancialData(blocks);
        var md = extractor.GenerateFinancialSummaryMarkdown(financialData);
        sb.AppendLine(md);

        // Also output the markdown conversion for the quarterly table block
        sb.AppendLine("\n====== Markdown转换输出(分季度部分) ======");
        var mdConverter = new MarkdownConverterService();
        // Find and convert just the quarterly block
        for (int i = 0; i < blocks.Count; i++)
        {
            if (blocks[i].Text.Contains("分季度主要财务数据"))
            {
                var testSb = new StringBuilder();
                var testBlocks = new List<PdfContentBlock> { blocks[i] };
                var result = mdConverter.ConvertToMarkdown(testBlocks);
                sb.AppendLine(result);
                break;
            }
        }

        var outputPath = Path.Combine(Path.GetDirectoryName(pdfPath)!, "test_output.txt");
        File.WriteAllText(outputPath, sb.ToString(), Encoding.UTF8);
    }
}
