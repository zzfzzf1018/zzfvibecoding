using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using PdfToMarkdown.Models;

namespace PdfToMarkdown.Services
{
    public class FinancialDataExtractor
    {
        // Key financial terms to look for in Chinese annual reports
        private static readonly Dictionary<string, string> FinancialTerms = new()
        {
            { "营业收入|营业总收入|主营业务收入", "Revenue" },
            { "归属于母公司.*?净利润|归属于上市公司股东的净利润|净利润", "NetProfit" },
            { "毛利|毛利润|营业毛利", "GrossProfit" },
            { "资产总[额计]|总资产", "TotalAssets" },
            { "负债[总合][额计]|总负债", "TotalLiabilities" },
            { "归属于母公司.*?所有者权益|净资产|股东权益", "NetAssets" },
            { "基本每股收益", "EarningsPerShare" },
            { "经营活动产生的现金流量净额", "OperatingCashFlow" }
        };

        public FinancialData ExtractFinancialData(List<PdfContentBlock> blocks)
        {
            var data = new FinancialData();

            // Extract basic info
            var parser = new PdfParserService();
            var (companyName, stockCode, reportYear) = parser.ExtractBasicInfo(blocks);
            data.CompanyName = companyName;
            data.StockCode = stockCode;
            data.ReportYear = reportYear;

            // Look for main financial summary tables
            ExtractMainFinancialMetrics(blocks, data);

            // Try to extract quarterly data
            ExtractQuarterlyData(blocks, data);

            return data;
        }

        private void ExtractMainFinancialMetrics(List<PdfContentBlock> blocks, FinancialData data)
        {
            // Look for the "主要会计数据" or "主要财务指标" sections
            bool inFinancialSection = false;

            for (int i = 0; i < blocks.Count; i++)
            {
                var block = blocks[i];

                if (block.Text.Contains("主要会计数据") || block.Text.Contains("主要财务指标"))
                {
                    inFinancialSection = true;
                    continue;
                }

                if (inFinancialSection && (block.Type == BlockType.Heading || block.Type == BlockType.Title))
                {
                    // Exiting the financial section
                    if (!block.Text.Contains("主要会计数据") && !block.Text.Contains("主要财务指标")
                        && !block.Text.Contains("季度"))
                    {
                        inFinancialSection = false;
                        continue;
                    }
                }

                if (inFinancialSection)
                {
                    foreach (var term in FinancialTerms)
                    {
                        if (Regex.IsMatch(block.Text, term.Key))
                        {
                            var value = ExtractNumericValue(block.Text, term.Key);
                            if (!string.IsNullOrEmpty(value))
                            {
                                data.KeyMetrics[term.Value] = value;
                            }
                        }
                    }

                    // Also check table data
                    if (block.TableData != null)
                    {
                        ExtractMetricsFromTable(block.TableData, data);
                    }
                }
            }
        }

        private void ExtractQuarterlyData(List<PdfContentBlock> blocks, FinancialData data)
        {
            // Strategy 1: Find the block that contains "分季度" - in A-stock reports, 
            // the entire table often ends up in one text block
            for (int i = 0; i < blocks.Count; i++)
            {
                var block = blocks[i];

                if (block.Text.Contains("分季度主要财务数据") || block.Text.Contains("分季度主要财务指标"))
                {
                    // The table data might be in this same block
                    if (TryExtractQuarterlyFromMergedBlock(block.Text, data))
                        return;

                    // Or in the next few blocks
                    for (int j = i + 1; j < Math.Min(i + 5, blocks.Count); j++)
                    {
                        if (blocks[j].Type == BlockType.PageBreak) continue;
                        if (TryExtractQuarterlyFromMergedBlock(blocks[j].Text, data))
                            return;
                        if (blocks[j].TableData != null && blocks[j].TableData.Count > 0)
                        {
                            ExtractQuarterlyFromTable(blocks[j].TableData, data);
                            if (data.QuarterlyData.Count > 0) return;
                        }
                    }
                }
            }

            // Strategy 2: Look for table blocks with quarterly headers
            if (data.QuarterlyData.Count == 0)
            {
                for (int i = 0; i < blocks.Count; i++)
                {
                    var block = blocks[i];
                    if (block.TableData != null && ContainsQuarterlyHeaders(block.TableData))
                    {
                        ExtractQuarterlyFromTable(block.TableData, data);
                        if (data.QuarterlyData.Count > 0)
                            break;
                    }
                }
            }
        }

        /// <summary>
        /// Handles the common case in A-stock annual reports where the quarterly table
        /// is extracted as a single merged text block with numbers interspersed.
        /// Pattern: each financial metric is followed by exactly 4 numbers (Q1-Q4).
        /// </summary>
        private bool TryExtractQuarterlyFromMergedBlock(string text, FinancialData data)
        {
            // Must have quarterly keywords and numbers
            if (!Regex.IsMatch(text, @"第[一二三四]季度|[1-4]季度"))
                return false;
            if (!text.Contains("营业收入"))
                return false;

            // Extract all large numbers (financial figures with commas)
            var allNumbers = Regex.Matches(text, @"(?<!\d)[\-]?\d{1,3}(?:,\d{3})+\.\d{2}(?!\d)")
                .Select(m => m.Value)
                .ToList();

            if (allNumbers.Count < 4)
                return false;

            // Initialize quarters
            var q1 = new QuarterlyFinancial { Period = "Q1" };
            var q2 = new QuarterlyFinancial { Period = "Q2" };
            var q3 = new QuarterlyFinancial { Period = "Q3" };
            var q4 = new QuarterlyFinancial { Period = "Q4" };

            // In the typical A-stock report format, numbers appear in groups of 4
            // corresponding to Q1, Q2, Q3, Q4 for each metric row.
            // The metrics order is typically: 营业收入, 净利润, 扣非净利润, 经营现金流
            
            // Determine column order from text: check if Q1 comes first or Q4 comes first
            // Usually: 第一季度 第二季度 第三季度 第四季度 (left to right)
            int[] order = { 0, 1, 2, 3 }; // Q1, Q2, Q3, Q4

            // Check actual order in text
            var q1Pos = FindFirstPosition(text, @"第一季度|（1-3");
            var q4Pos = FindFirstPosition(text, @"第四季度|（10-12");
            if (q1Pos >= 0 && q4Pos >= 0 && q4Pos < q1Pos)
            {
                order = new[] { 3, 2, 1, 0 }; // reversed
            }

            int numIdx = 0;
            int numGroups = allNumbers.Count / 4;

            // Map groups of 4 numbers to financial metrics
            if (numGroups >= 1 && numIdx + 3 < allNumbers.Count)
            {
                // 营业收入
                q1.Revenue = ParseChineseNumber(allNumbers[numIdx + order[0]]);
                q2.Revenue = ParseChineseNumber(allNumbers[numIdx + order[1]]);
                q3.Revenue = ParseChineseNumber(allNumbers[numIdx + order[2]]);
                q4.Revenue = ParseChineseNumber(allNumbers[numIdx + order[3]]);
                numIdx += 4;
            }
            if (numGroups >= 2 && numIdx + 3 < allNumbers.Count)
            {
                // 归属净利润
                q1.NetProfit = ParseChineseNumber(allNumbers[numIdx + order[0]]);
                q2.NetProfit = ParseChineseNumber(allNumbers[numIdx + order[1]]);
                q3.NetProfit = ParseChineseNumber(allNumbers[numIdx + order[2]]);
                q4.NetProfit = ParseChineseNumber(allNumbers[numIdx + order[3]]);
                numIdx += 4;
            }
            if (numGroups >= 3 && numIdx + 3 < allNumbers.Count)
            {
                // 扣非净利润 (store as GrossProfit field for now)
                q1.GrossProfit = ParseChineseNumber(allNumbers[numIdx + order[0]]);
                q2.GrossProfit = ParseChineseNumber(allNumbers[numIdx + order[1]]);
                q3.GrossProfit = ParseChineseNumber(allNumbers[numIdx + order[2]]);
                q4.GrossProfit = ParseChineseNumber(allNumbers[numIdx + order[3]]);
                numIdx += 4;
            }
            if (numGroups >= 4 && numIdx + 3 < allNumbers.Count)
            {
                // 经营活动现金流
                q1.OperatingCashFlow = ParseChineseNumber(allNumbers[numIdx + order[0]]);
                q2.OperatingCashFlow = ParseChineseNumber(allNumbers[numIdx + order[1]]);
                q3.OperatingCashFlow = ParseChineseNumber(allNumbers[numIdx + order[2]]);
                q4.OperatingCashFlow = ParseChineseNumber(allNumbers[numIdx + order[3]]);
                numIdx += 4;
            }

            q1.RawText = $"营业收入:{q1.Revenue} 净利润:{q1.NetProfit} 扣非净利润:{q1.GrossProfit} 经营现金流:{q1.OperatingCashFlow}";
            q2.RawText = $"营业收入:{q2.Revenue} 净利润:{q2.NetProfit} 扣非净利润:{q2.GrossProfit} 经营现金流:{q2.OperatingCashFlow}";
            q3.RawText = $"营业收入:{q3.Revenue} 净利润:{q3.NetProfit} 扣非净利润:{q3.GrossProfit} 经营现金流:{q3.OperatingCashFlow}";
            q4.RawText = $"营业收入:{q4.Revenue} 净利润:{q4.NetProfit} 扣非净利润:{q4.GrossProfit} 经营现金流:{q4.OperatingCashFlow}";

            data.QuarterlyData.Add(q1);
            data.QuarterlyData.Add(q2);
            data.QuarterlyData.Add(q3);
            data.QuarterlyData.Add(q4);

            return data.QuarterlyData.Any(q => q.Revenue != 0 || q.NetProfit != 0);
        }

        private int FindFirstPosition(string text, string pattern)
        {
            var match = Regex.Match(text, pattern);
            return match.Success ? match.Index : -1;
        }

        private bool ContainsQuarterlyPattern(string text)
        {
            return Regex.IsMatch(text, @"第[一二三四]季度|[1-4]季度|Q[1-4]|一季|二季|三季|四季");
        }

        private bool ContainsQuarterlyHeaders(List<List<string>> table)
        {
            if (table.Count == 0) return false;
            var headerText = string.Join(" ", table[0]);
            return ContainsQuarterlyPattern(headerText);
        }

        private void ExtractQuarterlyFromTable(List<List<string>> table, FinancialData data)
        {
            if (table.Count < 2) return;

            var headers = table[0];

            // Find quarter column indices
            var quarterIndices = new Dictionary<int, string>();
            for (int col = 0; col < headers.Count; col++)
            {
                if (Regex.IsMatch(headers[col], @"第?一季度?|Q1|1季"))
                    quarterIndices[col] = "Q1";
                else if (Regex.IsMatch(headers[col], @"第?二季度?|Q2|2季"))
                    quarterIndices[col] = "Q2";
                else if (Regex.IsMatch(headers[col], @"第?三季度?|Q3|3季"))
                    quarterIndices[col] = "Q3";
                else if (Regex.IsMatch(headers[col], @"第?四季度?|Q4|4季"))
                    quarterIndices[col] = "Q4";
            }

            if (quarterIndices.Count == 0)
            {
                // Maybe quarters are in rows instead of columns
                ExtractQuarterlyFromRows(table, data);
                return;
            }

            // Extract data for each quarter
            foreach (var qi in quarterIndices)
            {
                var quarterly = new QuarterlyFinancial { Period = qi.Value };
                var rawParts = new List<string>();

                for (int row = 1; row < table.Count; row++)
                {
                    if (qi.Key >= table[row].Count) continue;

                    string rowLabel = table[row][0];
                    string value = table[row][qi.Key];
                    rawParts.Add($"{rowLabel}: {value}");

                    decimal numValue = ParseChineseNumber(value);

                    if (Regex.IsMatch(rowLabel, "营业收入|营业总收入"))
                        quarterly.Revenue = numValue;
                    else if (Regex.IsMatch(rowLabel, "归属.*净利润|净利润"))
                        quarterly.NetProfit = numValue;
                    else if (Regex.IsMatch(rowLabel, "毛利"))
                        quarterly.GrossProfit = numValue;
                    else if (Regex.IsMatch(rowLabel, "基本每股收益|每股收益"))
                        quarterly.EarningsPerShare = numValue;
                    else if (Regex.IsMatch(rowLabel, "经营活动.*现金流"))
                        quarterly.OperatingCashFlow = numValue;
                }

                quarterly.RawText = string.Join("\n", rawParts);

                if (quarterly.Revenue != 0 || quarterly.NetProfit != 0)
                    data.QuarterlyData.Add(quarterly);
            }
        }

        private void ExtractQuarterlyFromRows(List<List<string>> table, FinancialData data)
        {
            foreach (var row in table)
            {
                if (row.Count < 2) continue;
                string label = row[0];

                string? quarter = null;
                if (Regex.IsMatch(label, @"第?一季度?|Q1|1季"))
                    quarter = "Q1";
                else if (Regex.IsMatch(label, @"第?二季度?|Q2|2季"))
                    quarter = "Q2";
                else if (Regex.IsMatch(label, @"第?三季度?|Q3|3季"))
                    quarter = "Q3";
                else if (Regex.IsMatch(label, @"第?四季度?|Q4|4季"))
                    quarter = "Q4";

                if (quarter != null)
                {
                    var quarterly = data.QuarterlyData.FirstOrDefault(q => q.Period == quarter);
                    if (quarterly == null)
                    {
                        quarterly = new QuarterlyFinancial { Period = quarter };
                        data.QuarterlyData.Add(quarterly);
                    }

                    // Try to extract values from remaining columns
                    for (int col = 1; col < row.Count; col++)
                    {
                        decimal val = ParseChineseNumber(row[col]);
                        if (val != 0)
                        {
                            if (quarterly.Revenue == 0)
                                quarterly.Revenue = val;
                            else if (quarterly.NetProfit == 0)
                                quarterly.NetProfit = val;
                            else if (quarterly.EarningsPerShare == 0 && Math.Abs(val) < 100)
                                quarterly.EarningsPerShare = val;
                        }
                    }

                    quarterly.RawText = string.Join(" | ", row);
                }
            }
        }

        private void ExtractQuarterlyFromText(string text, FinancialData data)
        {
            var lines = text.Split('\n');
            string currentQuarter = "";

            foreach (var line in lines)
            {
                if (Regex.IsMatch(line, @"第?一季度?|Q1"))
                    currentQuarter = "Q1";
                else if (Regex.IsMatch(line, @"第?二季度?|Q2"))
                    currentQuarter = "Q2";
                else if (Regex.IsMatch(line, @"第?三季度?|Q3"))
                    currentQuarter = "Q3";
                else if (Regex.IsMatch(line, @"第?四季度?|Q4"))
                    currentQuarter = "Q4";

                if (!string.IsNullOrEmpty(currentQuarter))
                {
                    var existing = data.QuarterlyData.FirstOrDefault(q => q.Period == currentQuarter);
                    if (existing == null)
                    {
                        existing = new QuarterlyFinancial { Period = currentQuarter };
                        data.QuarterlyData.Add(existing);
                    }

                    // Extract numbers from the line
                    var numbers = Regex.Matches(line, @"[\-\d,]+\.?\d*")
                        .Select(m => ParseChineseNumber(m.Value))
                        .Where(n => n != 0)
                        .ToList();

                    if (numbers.Count > 0 && existing.Revenue == 0)
                        existing.Revenue = numbers[0];
                    if (numbers.Count > 1 && existing.NetProfit == 0)
                        existing.NetProfit = numbers[1];

                    existing.RawText += line + "\n";
                }
            }
        }

        private void ExtractMetricsFromTable(List<List<string>> table, FinancialData data)
        {
            foreach (var row in table)
            {
                if (row.Count < 2) continue;
                string label = row[0];

                foreach (var term in FinancialTerms)
                {
                    if (Regex.IsMatch(label, term.Key))
                    {
                        // Take the first numeric value (current period)
                        for (int col = 1; col < row.Count; col++)
                        {
                            decimal val = ParseChineseNumber(row[col]);
                            if (val != 0)
                            {
                                data.KeyMetrics[term.Value] = row[col];
                                break;
                            }
                        }
                    }
                }
            }
        }

        private string ExtractNumericValue(string text, string termPattern)
        {
            // Try to find a number after the financial term
            var termMatch = Regex.Match(text, termPattern);
            if (termMatch.Success)
            {
                string after = text.Substring(termMatch.Index + termMatch.Length);
                // Look for formatted number (with commas) or plain number
                var numMatch = Regex.Match(after, @"[\-]?\d{1,3}(?:,\d{3})*\.?\d*");
                if (numMatch.Success)
                {
                    // Verify it's actually a number, not a partial match
                    decimal testVal = ParseChineseNumber(numMatch.Value);
                    if (testVal != 0)
                        return numMatch.Value;
                }
            }

            return string.Empty;
        }

        private decimal ParseChineseNumber(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return 0;

            // Remove commas and spaces
            text = text.Replace(",", "").Replace("，", "").Replace(" ", "").Trim();

            // Handle 万/亿 multipliers
            decimal multiplier = 1;
            if (text.EndsWith("万"))
            {
                multiplier = 10000;
                text = text.TrimEnd('万');
            }
            else if (text.EndsWith("亿"))
            {
                multiplier = 100000000;
                text = text.TrimEnd('亿');
            }
            else if (text.EndsWith("万元"))
            {
                multiplier = 10000;
                text = text.Replace("万元", "");
            }
            else if (text.EndsWith("元"))
            {
                text = text.TrimEnd('元');
            }

            // Handle percentage
            if (text.EndsWith("%") || text.EndsWith("％"))
            {
                text = text.TrimEnd('%', '％');
                if (decimal.TryParse(text, NumberStyles.Any, CultureInfo.InvariantCulture, out decimal pct))
                    return pct / 100;
            }

            if (decimal.TryParse(text, NumberStyles.Any, CultureInfo.InvariantCulture, out decimal result))
                return result * multiplier;

            return 0;
        }

        public string GenerateFinancialSummaryMarkdown(FinancialData data)
        {
            var sb = new StringBuilder();

            sb.AppendLine("# 财务数据摘要");
            sb.AppendLine();

            if (!string.IsNullOrEmpty(data.CompanyName))
                sb.AppendLine($"**公司名称**: {data.CompanyName}");
            if (!string.IsNullOrEmpty(data.StockCode))
                sb.AppendLine($"**股票代码**: {data.StockCode}");
            if (!string.IsNullOrEmpty(data.ReportYear))
                sb.AppendLine($"**报告年度**: {data.ReportYear}");
            sb.AppendLine();

            // Key metrics
            if (data.KeyMetrics.Count > 0)
            {
                sb.AppendLine("## 主要财务指标");
                sb.AppendLine();
                sb.AppendLine("| 指标 | 数值 |");
                sb.AppendLine("| --- | --- |");

                var metricNames = new Dictionary<string, string>
                {
                    { "Revenue", "营业收入" },
                    { "NetProfit", "归属母公司净利润" },
                    { "GrossProfit", "毛利润" },
                    { "TotalAssets", "总资产" },
                    { "TotalLiabilities", "总负债" },
                    { "NetAssets", "净资产" },
                    { "EarningsPerShare", "基本每股收益" },
                    { "OperatingCashFlow", "经营活动现金流" }
                };

                foreach (var metric in data.KeyMetrics)
                {
                    string displayName = metricNames.ContainsKey(metric.Key)
                        ? metricNames[metric.Key]
                        : metric.Key;
                    sb.AppendLine($"| {displayName} | {metric.Value} |");
                }
                sb.AppendLine();
            }

            // Quarterly data
            if (data.QuarterlyData.Count > 0)
            {
                sb.AppendLine("## 分季度财务数据");
                sb.AppendLine();
                sb.AppendLine("| 季度 | 营业收入 | 归属净利润 | 扣非净利润 | 经营现金流 |");
                sb.AppendLine("| --- | --- | --- | --- | --- |");

                foreach (var q in data.QuarterlyData.OrderBy(q => q.Period))
                {
                    sb.AppendLine($"| {q.Period} | {FormatNumber(q.Revenue)} | {FormatNumber(q.NetProfit)} | {FormatNumber(q.GrossProfit)} | {FormatNumber(q.OperatingCashFlow)} |");
                }
                sb.AppendLine();
            }

            return sb.ToString();
        }

        private string FormatNumber(decimal value)
        {
            if (value == 0) return "-";
            if (Math.Abs(value) >= 100000000)
                return $"{value / 100000000:F2}亿";
            if (Math.Abs(value) >= 10000)
                return $"{value / 10000:F2}万";
            return value.ToString("N2");
        }

        private string FormatDecimal(decimal value)
        {
            if (value == 0) return "-";
            return value.ToString("F4");
        }
    }
}
