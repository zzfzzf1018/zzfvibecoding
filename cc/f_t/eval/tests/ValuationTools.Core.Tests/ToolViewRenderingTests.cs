using System.Data;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ValuationTools.Desktop.ViewModels;
using ValuationTools.Desktop.ViewModels.Tools;
using ValuationTools.Desktop.Views;
using Xunit;

namespace ValuationTools.Core.Tests;

/// <summary>加载真实的 ToolView，验证表头与单元格能实际渲染出来（列名中的 . % \ 等字符曾导致绑定失效）。</summary>
[Collection("WPF")]
public class ToolViewRenderingTests
{
    private readonly WpfFixture _wpf;

    public ToolViewRenderingTests(WpfFixture wpf) => _wpf = wpf;

    public static IEnumerable<object[]> ToolTypes() => new List<object[]>
    {
        new object[] { typeof(DcfViewModel) },
        new object[] { typeof(DdmViewModel) },
        new object[] { typeof(ResidualIncomeViewModel) },
        new object[] { typeof(PegViewModel) },
        new object[] { typeof(RelativeValuationViewModel) },
        new object[] { typeof(GrahamViewModel) },
        new object[] { typeof(DiscountRateViewModel) },
        new object[] { typeof(GrowthViewModel) },
        new object[] { typeof(ProjectCashFlowViewModel) },
        new object[] { typeof(BondViewModel) },
        new object[] { typeof(OptionViewModel) }
    };

    [Theory]
    [MemberData(nameof(ToolTypes))]
    public void ToolView_RendersHeadersAndCells(Type toolType)
    {
        var failures = _wpf.Invoke(() =>
        {
            var tool = (ToolViewModel)Activator.CreateInstance(toolType)!;
            var window = new Window
            {
                Width = 1600,
                Height = 1200,
                Left = -20000,
                ShowInTaskbar = false,
                WindowStyle = WindowStyle.None,
                Content = new ToolView { DataContext = tool }
            };

            var problems = new List<string>();
            try
            {
                window.Show();
                window.UpdateLayout();

                foreach (var grid in FindDescendants<DataGrid>(window))
                {
                    if (grid.ItemsSource is not DataView view) continue;

                    var columns = view.Table!.Columns.Cast<DataColumn>().ToList();
                    if (grid.Columns.Count != columns.Count)
                    {
                        problems.Add($"{tool.Title}：生成 {grid.Columns.Count} 列，期望 {columns.Count} 列");
                        continue;
                    }

                    var renderedTexts = FindDescendants<TextBlock>(grid).Select(t => t.Text).ToHashSet();
                    for (int i = 0; i < columns.Count; i++)
                    {
                        if (!Equals(grid.Columns[i].Header, columns[i].Caption))
                            problems.Add($"{tool.Title}：第 {i} 列表头显示为「{grid.Columns[i].Header}」，期望「{columns[i].Caption}」");

                        string cell = (string)view[0][i];
                        if (!string.IsNullOrEmpty(cell) && !renderedTexts.Contains(cell))
                            problems.Add($"{tool.Title}：列「{columns[i].Caption}」首行未渲染出「{cell}」");
                    }
                }

                if (FindDescendants<TextBlock>(window).All(t => t.Text != tool.Title))
                    problems.Add($"{tool.Title}：标题未渲染");
            }
            finally
            {
                window.Close();
            }
            return problems;
        });

        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }

    private static IEnumerable<T> FindDescendants<T>(DependencyObject root) where T : DependencyObject
    {
        int count = VisualTreeHelper.GetChildrenCount(root);
        for (int i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T typed) yield return typed;
            foreach (var descendant in FindDescendants<T>(child)) yield return descendant;
        }
    }
}
