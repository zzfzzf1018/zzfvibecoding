using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using PdfToMarkdown.Models;

namespace PdfToMarkdown.Services
{
    public class MarkdownConverterService
    {
        public string ConvertToMarkdown(List<PdfContentBlock> blocks, string companyName = "", string reportYear = "")
        {
            var sb = new StringBuilder();

            if (!string.IsNullOrEmpty(companyName) || !string.IsNullOrEmpty(reportYear))
            {
                sb.AppendLine($"# {companyName} {reportYear}年度报告");
                sb.AppendLine();
            }

            int headingLevel = 1;

            foreach (var block in blocks)
            {
                // Special handling: detect merged quarterly financial table blocks
                if (TryConvertQuarterlyTableBlock(block, sb))
                    continue;

                switch (block.Type)
                {
                    case BlockType.Title:
                        sb.AppendLine($"# {CleanText(block.Text)}");
                        sb.AppendLine();
                        headingLevel = 1;
                        break;

                    case BlockType.Heading:
                        int level = DetermineHeadingLevel(block.Text, block.FontSize);
                        string prefix = new string('#', level);
                        sb.AppendLine($"{prefix} {CleanText(block.Text)}");
                        sb.AppendLine();
                        headingLevel = level;
                        break;

                    case BlockType.Table:
                        if (block.TableData != null && block.TableData.Count > 0)
                        {
                            sb.AppendLine(ConvertTableToMarkdown(block.TableData));
                            sb.AppendLine();
                        }
                        else
                        {
                            // Fallback: output as code block for alignment
                            sb.AppendLine("```");
                            sb.AppendLine(block.Text);
                            sb.AppendLine("```");
                            sb.AppendLine();
                        }
                        break;

                    case BlockType.List:
                        var lines = block.Text.Split('\n');
                        foreach (var line in lines)
                        {
                            string trimmed = line.Trim();
                            if (!string.IsNullOrEmpty(trimmed))
                            {
                                // Convert to markdown list
                                trimmed = Regex.Replace(trimmed, @"^[\d]+[\.\、\)]", "").Trim();
                                trimmed = Regex.Replace(trimmed, @"^[•·●○■□▪▫-]\s*", "").Trim();
                                sb.AppendLine($"- {trimmed}");
                            }
                        }
                        sb.AppendLine();
                        break;

                    case BlockType.Paragraph:
                        string text = CleanText(block.Text);
                        if (!string.IsNullOrWhiteSpace(text))
                        {
                            sb.AppendLine(text);
                            sb.AppendLine();
                        }
                        break;

                    case BlockType.PageBreak:
                        sb.AppendLine("---");
                        sb.AppendLine();
                        break;
                }
            }

            return sb.ToString();
        }

        private int DetermineHeadingLevel(string text, double fontSize)
        {
            // 第X章 -> h1
            if (Regex.IsMatch(text, @"^第[一二三四五六七八九十\d]+章"))
                return 1;
            // 第X节 -> h2
            if (Regex.IsMatch(text, @"^第[一二三四五六七八九十\d]+节"))
                return 2;
            // 一、二、-> h3
            if (Regex.IsMatch(text, @"^[一二三四五六七八九十]+、"))
                return 3;
            // (一)(二) -> h4
            if (Regex.IsMatch(text, @"^[（\(][一二三四五六七八九十\d]+[）\)]"))
                return 4;
            // 1. 2. -> h5
            if (Regex.IsMatch(text, @"^\d+[\.\、]"))
                return 5;

            // Fallback by font size
            if (fontSize >= 18) return 1;
            if (fontSize >= 15) return 2;
            if (fontSize >= 13) return 3;
            return 4;
        }

        private string ConvertTableToMarkdown(List<List<string>> tableData)
        {
            if (tableData.Count == 0) return string.Empty;

            var sb = new StringBuilder();

            // Normalize column count
            int maxCols = tableData.Max(r => r.Count);

            // Header row
            var header = tableData[0];
            while (header.Count < maxCols) header.Add("");

            sb.AppendLine("| " + string.Join(" | ", header.Select(EscapeMarkdownCell)) + " |");
            sb.AppendLine("| " + string.Join(" | ", Enumerable.Repeat("---", maxCols)) + " |");

            // Data rows
            for (int i = 1; i < tableData.Count; i++)
            {
                var row = tableData[i];
                while (row.Count < maxCols) row.Add("");
                sb.AppendLine("| " + string.Join(" | ", row.Select(EscapeMarkdownCell)) + " |");
            }

            return sb.ToString();
        }

        private string EscapeMarkdownCell(string cell)
        {
            return cell.Replace("|", "\\|").Replace("\n", " ").Trim();
        }

        private string CleanText(string text)
        {
            // Remove excessive whitespace
            text = Regex.Replace(text, @"\s+", " ").Trim();
            // Restore newlines for multiple sentences
            text = Regex.Replace(text, @"([。！？；])\s*", "$1\n");
            return text.Trim();
        }

        /// <summary>
        /// Detects a merged quarterly financial table block (common in A-stock annual reports)
        /// and converts it to a proper markdown table.
        /// </summary>
        private bool TryConvertQuarterlyTableBlock(PdfContentBlock block, StringBuilder sb)
        {
            if (!block.Text.Contains("分季度主要财务数据"))
                return false;
            if (!block.Text.Contains("营业收入"))
                return false;
            if (!Regex.IsMatch(block.Text, @"第[一二三四]季度"))
                return false;

            // Extract all large numbers (financial figures: xx,xxx,xxx.xx pattern)
            var allNumbers = Regex.Matches(block.Text, @"(?<!\d)[\-]?\d{1,3}(?:,\d{3})+\.\d{2}(?!\d)")
                .Select(m => m.Value)
                .ToList();

            if (allNumbers.Count < 8)
                return false;

            // Determine column order 
            int[] colOrder = { 0, 1, 2, 3 }; // Q1, Q2, Q3, Q4
            var q1Pos = Regex.Match(block.Text, @"第一季度|\(1-3|（1-3");
            var q4Pos = Regex.Match(block.Text, @"第四季度|\(10-12|（10-12");
            if (q1Pos.Success && q4Pos.Success && q4Pos.Index < q1Pos.Index)
            {
                colOrder = new[] { 3, 2, 1, 0 };
            }

            // Detect interleaved vs row-major layout
            bool isInterleaved = DetectInterleavedNumbers(allNumbers);

            // Extract title/heading
            var titleMatch = Regex.Match(block.Text, @"[一二三四五六七八九十]+、\s*\d{4}\s*年分季度主要财务数据");
            if (titleMatch.Success)
            {
                sb.AppendLine($"### {titleMatch.Value.Trim()}");
            }
            else
            {
                sb.AppendLine("### 分季度主要财务数据");
            }
            sb.AppendLine();

            // Determine unit
            string unit = "";
            if (block.Text.Contains("单位：元") || block.Text.Contains("单位:元") || block.Text.Contains("单位：元币种"))
                unit = "（单位：元）";
            else if (block.Text.Contains("单位：万元"))
                unit = "（单位：万元）";
            if (!string.IsNullOrEmpty(unit))
                sb.AppendLine(unit);
            sb.AppendLine();

            // Row labels for typical A-stock quarterly table
            var rowLabels = new[] { "营业收入", "归属于上市公司股东的净利润", "归属于上市公司股东的扣除非经常性损益后的净利润", "经营活动产生的现金流量净额" };

            // Build table
            sb.AppendLine("| 项目 | 第一季度（1-3月） | 第二季度（4-6月） | 第三季度（7-9月） | 第四季度（10-12月） |");
            sb.AppendLine("| --- | ---: | ---: | ---: | ---: |");

            if (isInterleaved)
            {
                // Interleaved: 2 rows × 4 columns grouped together
                // Pattern: R1_Q1, R2_Q1, R1_Q2, R2_Q2, R1_Q3, R2_Q3, R1_Q4, R2_Q4
                // First group (8 numbers): 营业收入 + 净利润
                // For first group: even positions are larger (revenue), odd are smaller (profit)
                // Revenue = even indices, Profit = odd indices
                {
                    int baseIdx = 0;
                    // Determine which positions (even/odd) are revenue vs profit
                    // Revenue should always be larger than profit
                    double avgEven = Enumerable.Range(0, 4).Select(c => Math.Abs(ParseNumber(allNumbers[baseIdx + c * 2]))).Average();
                    double avgOdd = Enumerable.Range(0, 4).Select(c => Math.Abs(ParseNumber(allNumbers[baseIdx + c * 2 + 1]))).Average();

                    int revenueOffset = avgEven >= avgOdd ? 0 : 1;
                    int profitOffset = 1 - revenueOffset;

                    string q1Rev = allNumbers[baseIdx + colOrder[0] * 2 + revenueOffset];
                    string q2Rev = allNumbers[baseIdx + colOrder[1] * 2 + revenueOffset];
                    string q3Rev = allNumbers[baseIdx + colOrder[2] * 2 + revenueOffset];
                    string q4Rev = allNumbers[baseIdx + colOrder[3] * 2 + revenueOffset];
                    sb.AppendLine($"| {rowLabels[0]} | {q1Rev} | {q2Rev} | {q3Rev} | {q4Rev} |");

                    string q1Prof = allNumbers[baseIdx + colOrder[0] * 2 + profitOffset];
                    string q2Prof = allNumbers[baseIdx + colOrder[1] * 2 + profitOffset];
                    string q3Prof = allNumbers[baseIdx + colOrder[2] * 2 + profitOffset];
                    string q4Prof = allNumbers[baseIdx + colOrder[3] * 2 + profitOffset];
                    sb.AppendLine($"| {rowLabels[1]} | {q1Prof} | {q2Prof} | {q3Prof} | {q4Prof} |");

                    // Second group (8 numbers): 扣非净利润 + 经营现金流
                    // 扣非净利润 ≈ 净利润 in magnitude
                    if (allNumbers.Count >= 16)
                    {
                        int baseIdx2 = 8;
                        double avgEven2 = Enumerable.Range(0, 4).Select(c => Math.Abs(ParseNumber(allNumbers[baseIdx2 + c * 2]))).Average();
                        double avgOdd2 = Enumerable.Range(0, 4).Select(c => Math.Abs(ParseNumber(allNumbers[baseIdx2 + c * 2 + 1]))).Average();
                        double avgProfitAbs = Enumerable.Range(0, 4).Select(c => Math.Abs(ParseNumber(allNumbers[baseIdx + colOrder[c] * 2 + profitOffset]))).Average();

                        // Which set is closer to profit magnitude? That's 扣非
                        double evenRatio2 = avgEven2 > 0 && avgProfitAbs > 0 ? Math.Max(avgEven2 / avgProfitAbs, avgProfitAbs / avgEven2) : double.MaxValue;
                        double oddRatio2 = avgOdd2 > 0 && avgProfitAbs > 0 ? Math.Max(avgOdd2 / avgProfitAbs, avgProfitAbs / avgOdd2) : double.MaxValue;

                        int deductedOffset = evenRatio2 <= oddRatio2 ? 0 : 1;
                        int cashFlowOffset = 1 - deductedOffset;

                        string q1Ded = allNumbers[baseIdx2 + colOrder[0] * 2 + deductedOffset];
                        string q2Ded = allNumbers[baseIdx2 + colOrder[1] * 2 + deductedOffset];
                        string q3Ded = allNumbers[baseIdx2 + colOrder[2] * 2 + deductedOffset];
                        string q4Ded = allNumbers[baseIdx2 + colOrder[3] * 2 + deductedOffset];
                        sb.AppendLine($"| {rowLabels[2]} | {q1Ded} | {q2Ded} | {q3Ded} | {q4Ded} |");

                        string q1Cf = allNumbers[baseIdx2 + colOrder[0] * 2 + cashFlowOffset];
                        string q2Cf = allNumbers[baseIdx2 + colOrder[1] * 2 + cashFlowOffset];
                        string q3Cf = allNumbers[baseIdx2 + colOrder[2] * 2 + cashFlowOffset];
                        string q4Cf = allNumbers[baseIdx2 + colOrder[3] * 2 + cashFlowOffset];
                        sb.AppendLine($"| {rowLabels[3]} | {q1Cf} | {q2Cf} | {q3Cf} | {q4Cf} |");
                    }
                }
            }
            else
            {
                // Row-major: 4 numbers per metric row
                int numGroups = allNumbers.Count / 4;
                for (int row = 0; row < Math.Min(numGroups, rowLabels.Length); row++)
                {
                    int baseIdx = row * 4;
                    if (baseIdx + 3 >= allNumbers.Count) break;

                    string q1Val = allNumbers[baseIdx + colOrder[0]];
                    string q2Val = allNumbers[baseIdx + colOrder[1]];
                    string q3Val = allNumbers[baseIdx + colOrder[2]];
                    string q4Val = allNumbers[baseIdx + colOrder[3]];

                    sb.AppendLine($"| {rowLabels[row]} | {q1Val} | {q2Val} | {q3Val} | {q4Val} |");
                }
            }

            sb.AppendLine();
            return true;
        }

        /// <summary>
        /// Detects if the first 8 numbers are interleaved (alternating between two magnitude levels).
        /// </summary>
        private bool DetectInterleavedNumbers(List<string> allNumbers)
        {
            if (allNumbers.Count < 8) return false;

            var values = allNumbers.Take(8)
                .Select(n => Math.Abs(ParseNumber(n)))
                .ToList();

            // Check row-major: are first 4 numbers of similar magnitude?
            double maxFirst4 = values.Take(4).Max();
            double minFirst4 = values.Take(4).Where(v => v > 0).DefaultIfEmpty(1).Min();
            if (maxFirst4 > 0 && minFirst4 > 0 && maxFirst4 / minFirst4 < 10)
                return false; // row-major

            // Check interleaved: even indices (0,2,4,6) similar AND odd indices (1,3,5,7) similar
            var evenValues = new[] { values[0], values[2], values[4], values[6] };
            var oddValues = new[] { values[1], values[3], values[5], values[7] };

            double maxEven = evenValues.Max();
            double minEven = evenValues.Where(v => v > 0).DefaultIfEmpty(1).Min();
            double maxOdd = oddValues.Max();
            double minOdd = oddValues.Where(v => v > 0).DefaultIfEmpty(1).Min();

            bool evenSimilar = maxEven > 0 && minEven > 0 && maxEven / minEven < 10;
            bool oddSimilar = maxOdd > 0 && minOdd > 0 && maxOdd / minOdd < 10;

            double avgEven = evenValues.Where(v => v > 0).DefaultIfEmpty(0).Average();
            double avgOdd = oddValues.Where(v => v > 0).DefaultIfEmpty(0).Average();
            bool differentMagnitude = avgEven > 0 && avgOdd > 0 &&
                (avgEven / avgOdd > 5 || avgOdd / avgEven > 5);

            return evenSimilar && oddSimilar && differentMagnitude;
        }

        private double ParseNumber(string text)
        {
            text = text.Replace(",", "").Trim();
            if (double.TryParse(text, System.Globalization.NumberStyles.Any, 
                System.Globalization.CultureInfo.InvariantCulture, out double result))
                return result;
            return 0;
        }
    }
}
