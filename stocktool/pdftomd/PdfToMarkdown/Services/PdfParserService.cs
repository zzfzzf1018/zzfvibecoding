using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.DocumentLayoutAnalysis.PageSegmenter;
using UglyToad.PdfPig.DocumentLayoutAnalysis.WordExtractor;
using PdfToMarkdown.Models;

namespace PdfToMarkdown.Services
{
    public class PdfParserService
    {
        public List<PdfContentBlock> ParsePdf(string filePath, IProgress<int>? progress = null)
        {
            var blocks = new List<PdfContentBlock>();

            using var document = PdfDocument.Open(filePath);
            int totalPages = document.NumberOfPages;

            for (int i = 1; i <= totalPages; i++)
            {
                var page = document.GetPage(i);
                var pageBlocks = ExtractPageBlocks(page, i);
                blocks.AddRange(pageBlocks);

                blocks.Add(new PdfContentBlock
                {
                    Type = BlockType.PageBreak,
                    PageNumber = i
                });

                progress?.Report((int)((double)i / totalPages * 100));
            }

            return blocks;
        }

        private List<PdfContentBlock> ExtractPageBlocks(Page page, int pageNumber)
        {
            var blocks = new List<PdfContentBlock>();
            var letters = page.Letters.ToList();

            if (!letters.Any())
                return blocks;

            var words = NearestNeighbourWordExtractor.Instance.GetWords(letters);
            var textBlocks = DocstrumBoundingBoxes.Instance.GetBlocks(words);

            foreach (var block in textBlocks)
            {
                var blockLetters = block.TextLines
                    .SelectMany(tl => tl.Words)
                    .SelectMany(w => w.Letters)
                    .ToList();

                double avgFontSize = blockLetters.Any()
                    ? blockLetters.Average(l => l.FontSize)
                    : 10;

                bool isBold = blockLetters.Any() &&
                    blockLetters.Count(l => l.Font.IsBold ||
                        l.Font.Name.Contains("Bold", StringComparison.OrdinalIgnoreCase) ||
                        l.Font.Name.Contains("Heavy", StringComparison.OrdinalIgnoreCase)) >
                    blockLetters.Count / 2;

                string text = block.Text.Trim();

                if (string.IsNullOrWhiteSpace(text))
                    continue;

                var contentBlock = new PdfContentBlock
                {
                    PageNumber = pageNumber,
                    FontSize = avgFontSize,
                    IsBold = isBold,
                    Text = text
                };

                // Detect type based on font size, boldness, and content
                if (IsTable(block, text))
                {
                    contentBlock.Type = BlockType.Table;
                    contentBlock.TableData = ParseTableFromText(text);
                }
                else if (IsTitle(avgFontSize, isBold, text, pageNumber))
                {
                    contentBlock.Type = BlockType.Title;
                }
                else if (IsHeading(avgFontSize, isBold, text))
                {
                    contentBlock.Type = BlockType.Heading;
                }
                else if (IsList(text))
                {
                    contentBlock.Type = BlockType.List;
                }
                else
                {
                    contentBlock.Type = BlockType.Paragraph;
                }

                blocks.Add(contentBlock);
            }

            return blocks;
        }

        private bool IsTitle(double fontSize, bool isBold, string text, int pageNumber)
        {
            // Titles are usually large, bold, and short
            if (fontSize >= 16 && isBold && text.Length < 100)
                return true;
            // First page large text
            if (pageNumber <= 2 && fontSize >= 14 && text.Length < 60)
                return true;
            return false;
        }

        private bool IsHeading(double fontSize, bool isBold, string text)
        {
            if (text.Length > 200)
                return false;
            if (fontSize >= 12 && isBold)
                return true;
            // Chinese numbered headings: 一、 二、 第一节 第一章
            if (Regex.IsMatch(text, @"^[一二三四五六七八九十]+、") ||
                Regex.IsMatch(text, @"^第[一二三四五六七八九十\d]+[节章条款]") ||
                Regex.IsMatch(text, @"^[（\(][一二三四五六七八九十\d]+[）\)]") ||
                Regex.IsMatch(text, @"^\d+[\.\、]"))
                return true;
            return false;
        }

        private bool IsList(string text)
        {
            var lines = text.Split('\n');
            if (lines.Length < 2) return false;

            int listLineCount = lines.Count(l =>
                Regex.IsMatch(l.Trim(), @"^[\d]+[\.\、\)]") ||
                Regex.IsMatch(l.Trim(), @"^[•·●○■□▪▫-]") ||
                Regex.IsMatch(l.Trim(), @"^[（\(][一二三四五六七八九十\d]+[）\)]"));

            return listLineCount >= lines.Length / 2;
        }

        private bool IsTable(UglyToad.PdfPig.DocumentLayoutAnalysis.TextBlock block, string text)
        {
            var lines = text.Split('\n');
            if (lines.Length < 2) return false;

            // Check for consistent delimiter patterns (spaces, tabs)
            int linesWithMultipleColumns = 0;
            foreach (var line in lines)
            {
                // Multiple consecutive spaces or tabs suggest columns
                if (Regex.IsMatch(line, @"\s{3,}") || line.Contains('\t'))
                    linesWithMultipleColumns++;
                // Or number patterns typical in financial tables
                if (Regex.IsMatch(line, @"[\d,]+\.?\d*\s+[\d,]+\.?\d*"))
                    linesWithMultipleColumns++;
            }

            return linesWithMultipleColumns >= lines.Length * 0.5;
        }

        private List<List<string>> ParseTableFromText(string text)
        {
            var table = new List<List<string>>();
            var lines = text.Split('\n');

            foreach (var line in lines)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;

                // Split by multiple spaces or tabs
                var cells = Regex.Split(line.Trim(), @"\s{2,}|\t+")
                    .Where(c => !string.IsNullOrWhiteSpace(c))
                    .Select(c => c.Trim())
                    .ToList();

                if (cells.Count > 0)
                    table.Add(cells);
            }

            return table;
        }

        public (string companyName, string stockCode, string reportYear) ExtractBasicInfo(List<PdfContentBlock> blocks)
        {
            string companyName = "";
            string stockCode = "";
            string reportYear = "";

            // Search in first few blocks for company info
            var firstBlocks = blocks.Take(20).ToList();

            foreach (var block in firstBlocks)
            {
                // Stock code pattern: 6 digits
                var codeMatch = Regex.Match(block.Text, @"(?:股票代码|证券代码)[：:\s]*(\d{6})");
                if (codeMatch.Success)
                    stockCode = codeMatch.Groups[1].Value;

                // Company name
                var nameMatch = Regex.Match(block.Text, @"(?:公司名称|公司简称)[：:\s]*(.+?)(?:\s|$)");
                if (nameMatch.Success)
                    companyName = nameMatch.Groups[1].Value.Trim();

                // Report year
                var yearMatch = Regex.Match(block.Text, @"(\d{4})\s*年[度]?\s*(?:年度报告|年报|annual\s*report)", RegexOptions.IgnoreCase);
                if (yearMatch.Success)
                    reportYear = yearMatch.Groups[1].Value;

                if (string.IsNullOrEmpty(reportYear))
                {
                    yearMatch = Regex.Match(block.Text, @"(\d{4})\s*(?:年度报告|年报)");
                    if (yearMatch.Success)
                        reportYear = yearMatch.Groups[1].Value;
                }

                // Try to extract company name from title
                if (string.IsNullOrEmpty(companyName) && block.Type == BlockType.Title)
                {
                    var titleNameMatch = Regex.Match(block.Text, @"(.+?)(?:股份有限公司|有限公司)");
                    if (titleNameMatch.Success)
                        companyName = titleNameMatch.Groups[0].Value.Trim();
                }
            }

            return (companyName, stockCode, reportYear);
        }
    }
}
