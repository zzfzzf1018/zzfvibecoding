using System.Windows;
using System.Windows.Controls;
using ScottPlot;
using StockAnalyzer.Core.Models;

namespace StockAnalyzer.Desktop.Charts;

/// <summary>估值通道图：股价走势 + 各分位对应的价格带。</summary>
public partial class ValuationChannelChart : UserControl
{
    public static readonly DependencyProperty ChannelProperty = DependencyProperty.Register(
        nameof(Channel),
        typeof(ValuationChannel),
        typeof(ValuationChannelChart),
        new PropertyMetadata(null, OnChannelChanged));

    public ValuationChannelChart()
    {
        InitializeComponent();
        ChartStyler.ApplyDarkTheme(PlotControl.Plot);
        PlotControl.Refresh();
    }

    public ValuationChannel? Channel
    {
        get => (ValuationChannel?)GetValue(ChannelProperty);
        set => SetValue(ChannelProperty, value);
    }

    private static void OnChannelChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ValuationChannelChart chart)
        {
            chart.Redraw();
        }
    }

    private void Redraw()
    {
        Plot plot = PlotControl.Plot;
        plot.Clear();
        ChartStyler.ApplyDarkTheme(plot);

        ValuationChannel? channel = Channel;

        if (channel is null || channel.Dates.Count == 0)
        {
            plot.Title("暂无数据");
            ChartStyler.ApplyChineseFont(plot);
            PlotControl.Refresh();
            return;
        }

        DateTime[] dates = channel.Dates.ToArray();

        for (int i = 0; i < channel.Bands.Count; i++)
        {
            ValuationBand band = channel.Bands[i];
            (DateTime[] xs, double[] ys) = FilterValid(dates, band.Prices);

            if (xs.Length == 0)
            {
                continue;
            }

            var line = plot.Add.Scatter(xs, ys);
            line.MarkerSize = 0;
            line.LineWidth = 1.4f;
            line.Color = ChartStyler.BandColor(i, channel.Bands.Count);
            line.LegendText = band.Label;
        }

        var priceLine = plot.Add.Scatter(dates, channel.Close.ToArray());
        priceLine.MarkerSize = 0;
        priceLine.LineWidth = 2.2f;
        priceLine.Color = ChartStyler.Price;
        priceLine.LegendText = "收盘价（不复权）";

        plot.Axes.DateTimeTicksBottom();
        plot.YLabel($"价格（{(channel.Market == MarketType.HongKong ? "港元" : "元")}）");
        plot.Title($"{channel.Name} {channel.Code} · {channel.MetricName}估值通道 · {channel.Window.ToDisplayName()}");
        plot.ShowLegend(Alignment.UpperLeft);
        plot.Axes.AutoScale();
        ChartStyler.ApplyChineseFont(plot);

        PlotControl.Refresh();
    }

    private static (DateTime[] Xs, double[] Ys) FilterValid(DateTime[] dates, IReadOnlyList<double?> values)
    {
        var xs = new List<DateTime>(dates.Length);
        var ys = new List<double>(dates.Length);

        for (int i = 0; i < dates.Length && i < values.Count; i++)
        {
            if (values[i] is { } value && !double.IsNaN(value) && !double.IsInfinity(value))
            {
                xs.Add(dates[i]);
                ys.Add(value);
            }
        }

        return (xs.ToArray(), ys.ToArray());
    }
}
