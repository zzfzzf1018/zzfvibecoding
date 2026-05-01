using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Win32;
using PdfToMarkdown.Models;
using PdfToMarkdown.Services;
using UglyToad.PdfPig;

namespace PdfToMarkdown;

public partial class MainWindow : Window
{
    private readonly PdfParserService _pdfParser = new();
    private readonly MarkdownConverterService _markdownConverter = new();
    private readonly FinancialDataExtractor _financialExtractor = new();

    private string _currentFilePath = string.Empty;
    private List<PdfContentBlock>? _currentBlocks;
    private string _currentMarkdown = string.Empty;
    private string _currentFinancialMarkdown = string.Empty;

    public MainWindow()
    {
        InitializeComponent();
    }

    private void BtnSelectFile_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Filter = "PDF文件 (*.pdf)|*.pdf|所有文件 (*.*)|*.*",
            Title = "选择PDF文件"
        };

        if (dialog.ShowDialog() == true)
        {
            _currentFilePath = dialog.FileName;
            TxtFilePath.Text = _currentFilePath;
            TxtFilePath.Foreground = System.Windows.Media.Brushes.Black;
            BtnConvert.IsEnabled = true;
            BtnExtract.IsEnabled = true;

            ShowPdfInfo(_currentFilePath);
        }
    }

    private void ShowPdfInfo(string filePath)
    {
        try
        {
            using var doc = PdfDocument.Open(filePath);
            var info = new StringBuilder();
            info.AppendLine($"文件名: {System.IO.Path.GetFileName(filePath)}");
            info.AppendLine($"文件大小: {new FileInfo(filePath).Length / 1024.0 / 1024.0:F2} MB");
            info.AppendLine($"页数: {doc.NumberOfPages}");

            if (doc.Information != null)
            {
                if (!string.IsNullOrEmpty(doc.Information.Title))
                    info.AppendLine($"标题: {doc.Information.Title}");
                if (!string.IsNullOrEmpty(doc.Information.Author))
                    info.AppendLine($"作者: {doc.Information.Author}");
                if (!string.IsNullOrEmpty(doc.Information.Creator))
                    info.AppendLine($"创建工具: {doc.Information.Creator}");
                if (!string.IsNullOrEmpty(doc.Information.Producer))
                    info.AppendLine($"PDF生成器: {doc.Information.Producer}");
            }

            TxtInfo.Text = info.ToString();
            UpdateStatus($"已加载PDF文件: {doc.NumberOfPages} 页");
        }
        catch (Exception ex)
        {
            TxtInfo.Text = $"无法读取PDF信息: {ex.Message}";
            UpdateStatus("读取PDF信息失败");
        }
    }

    private async void BtnConvert_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_currentFilePath))
        {
            MessageBox.Show("请先选择PDF文件", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        SetBusy(true, "正在转换PDF...");

        try
        {
            var progress = new Progress<int>(value =>
            {
                ProgressBar.Value = value;
                UpdateStatus($"正在解析PDF... {value}%");
            });

            _currentBlocks = await Task.Run(() => _pdfParser.ParsePdf(_currentFilePath, progress));

            var (companyName, stockCode, reportYear) = _pdfParser.ExtractBasicInfo(_currentBlocks);
            _currentMarkdown = _markdownConverter.ConvertToMarkdown(_currentBlocks, companyName, reportYear);

            TxtMarkdown.Text = _currentMarkdown;
            TxtPreview.Text = GeneratePreviewText(_currentMarkdown);
            BtnSave.IsEnabled = true;

            UpdateStatus($"转换完成! 共 {_currentBlocks.Count} 个内容块, 公司: {companyName}, 年度: {reportYear}");
        }
        catch (Exception ex)
        {
            MessageBox.Show($"转换失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            UpdateStatus("转换失败");
        }
        finally
        {
            SetBusy(false);
        }
    }

    private async void BtnExtractFinancial_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_currentFilePath))
        {
            MessageBox.Show("请先选择PDF文件", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        SetBusy(true, "正在提取财务数据...");

        try
        {
            if (_currentBlocks == null)
            {
                var progress = new Progress<int>(value =>
                {
                    ProgressBar.Value = value;
                    UpdateStatus($"正在解析PDF... {value}%");
                });

                _currentBlocks = await Task.Run(() => _pdfParser.ParsePdf(_currentFilePath, progress));
            }

            var financialData = await Task.Run(() => _financialExtractor.ExtractFinancialData(_currentBlocks));
            _currentFinancialMarkdown = _financialExtractor.GenerateFinancialSummaryMarkdown(financialData);

            TxtFinancial.Text = _currentFinancialMarkdown;
            TabMain.SelectedIndex = 1; // Switch to financial tab
            BtnSave.IsEnabled = true;

            int quarterCount = financialData.QuarterlyData.Count;
            int metricCount = financialData.KeyMetrics.Count;
            UpdateStatus($"财务数据提取完成! 发现 {metricCount} 项指标, {quarterCount} 个季度数据");
        }
        catch (Exception ex)
        {
            MessageBox.Show($"提取失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            UpdateStatus("财务数据提取失败");
        }
        finally
        {
            SetBusy(false);
        }
    }

    private void BtnSave_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog
        {
            Filter = "Markdown文件 (*.md)|*.md|文本文件 (*.txt)|*.txt",
            Title = "保存Markdown文件",
            FileName = System.IO.Path.GetFileNameWithoutExtension(_currentFilePath) + ".md"
        };

        if (dialog.ShowDialog() == true)
        {
            try
            {
                var content = new StringBuilder();

                if (!string.IsNullOrEmpty(_currentMarkdown))
                    content.Append(_currentMarkdown);

                if (!string.IsNullOrEmpty(_currentFinancialMarkdown))
                {
                    content.AppendLine();
                    content.AppendLine("---");
                    content.AppendLine();
                    content.Append(_currentFinancialMarkdown);
                }

                File.WriteAllText(dialog.FileName, content.ToString(), Encoding.UTF8);
                UpdateStatus($"文件已保存: {dialog.FileName}");
                MessageBox.Show("保存成功!", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"保存失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private string GeneratePreviewText(string markdown)
    {
        // Simple markdown-to-plain-text preview
        var lines = markdown.Split('\n');
        var sb = new StringBuilder();

        foreach (var line in lines)
        {
            string processed = line;
            // Remove markdown heading markers
            if (processed.StartsWith('#'))
            {
                processed = processed.TrimStart('#').Trim();
                processed = $"【{processed}】";
            }
            // Remove table markers
            processed = processed.Replace("|", " │ ");
            if (processed.Trim().StartsWith("---"))
                processed = "────────────────────";

            sb.AppendLine(processed);
        }

        return sb.ToString();
    }

    private void SetBusy(bool busy, string message = "")
    {
        BtnConvert.IsEnabled = !busy && !string.IsNullOrEmpty(_currentFilePath);
        BtnExtract.IsEnabled = !busy && !string.IsNullOrEmpty(_currentFilePath);
        ProgressBar.Visibility = busy ? Visibility.Visible : Visibility.Collapsed;

        if (busy)
        {
            ProgressBar.Value = 0;
            UpdateStatus(message);
        }
    }

    private void UpdateStatus(string message)
    {
        TxtStatus.Text = message;
    }
}