using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using ComparetoolWpf.Converters;
using ComparetoolWpf.Models;
using ComparetoolWpf.ViewModels;

namespace ComparetoolWpf.Views;

/// <summary>
/// 多股横向对比视图。由于股票数量动态变化，DataGrid 列需要在代码后置中按需重建。
/// </summary>
public partial class MultiStockCompareView : UserControl
{
    private static readonly NullableDoubleConverter NumConv = new();
    private static readonly PercentConverter PctConv = new();

    public MultiStockCompareView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object? sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is MultiStockCompareViewModel oldVm)
            oldVm.ColumnsRebuildRequested -= OnRebuild;
        if (e.NewValue is MultiStockCompareViewModel newVm)
            newVm.ColumnsRebuildRequested += OnRebuild;
    }

    private void OnRebuild(object? sender, EventArgs e)
    {
        if (DataContext is not MultiStockCompareViewModel vm) return;

        ResultGrid.Columns.Clear();
        // 第一列：指标
        ResultGrid.Columns.Add(new DataGridTextColumn
        {
            Header = "指标",
            Binding = new Binding(nameof(MultiStockRow.Item)),
            Width = new DataGridLength(220),
        });

        // 每只股票两列：原始数值 + 占比
        foreach (var stock in vm.ColumnsStocks)
        {
            string key = stock.FullCode;
            // 数值
            var rawCol = new DataGridTextColumn
            {
                Header = $"{stock.Name}\n数值",
                Width = new DataGridLength(140),
                Binding = new Binding($"RawValues[{key}]") { Converter = NumConv },
            };
            rawCol.ElementStyle = MakeRightAlign();
            ResultGrid.Columns.Add(rawCol);

            // 占比
            var pctCol = new DataGridTextColumn
            {
                Header = $"{stock.Name}\n占比",
                Width = new DataGridLength(110),
                Binding = new Binding($"Percentages[{key}]") { Converter = PctConv },
            };
            pctCol.ElementStyle = MakeRightAlign();
            ResultGrid.Columns.Add(pctCol);
        }
    }

    private static Style MakeRightAlign()
    {
        var s = new Style(typeof(TextBlock));
        s.Setters.Add(new Setter(TextBlock.HorizontalAlignmentProperty, HorizontalAlignment.Right));
        return s;
    }
}
