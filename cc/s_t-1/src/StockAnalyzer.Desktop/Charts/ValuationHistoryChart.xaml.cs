using System.Windows;
using System.Windows.Controls;
using ScottPlot;
using StockAnalyzer.Core.Analytics;
using StockAnalyzer.Core.Models;
using StockAnalyzer.Desktop.Infrastructure;

namespace StockAnalyzer.Desktop.Charts;

/// <summary>PE / PB 历史走势图，附带分位水平线。</summary>
public partial class ValuationHistoryChart : UserControl
{
    private static readonly double[] ReferenceQuantiles = { 0.1, 0.3, 0.5, 0.7, 0.9 };

    public static readonly DependencyProperty SeriesProperty = DependencyProperty.Register(
        nameof(Series),
        typeof(ValuationSeries),
        typeof(ValuationHistoryChart),
        new PropertyMetadata(null, OnInputChanged));

    public static readonly DependencyProperty MetricOptionProperty = DependencyProperty.Register(
        nameof(MetricOption),
        typeof(EnumOption<ValuationMetric>),
        typeof(ValuationHistoryChart),
        new PropertyMetadata(null, OnInputChanged));

    public static readonly DependencyProperty WindowOptionProperty = DependencyProperty.Register(
        nameof(WindowOption),
        typeof(EnumOption<LookbackWindow>),
        typeof(ValuationHistoryChart),
        new PropertyMetadata(null, OnInputChanged));

    public ValuationHistoryChart()
    {
        InitializeComponent();
        ChartStyler.ApplyDarkTheme(PlotControl.Plot);
        PlotControl.Refresh();
    }

    public ValuationSeries? Series
    {
        get => (ValuationSeries?)GetValue(SeriesProperty);
        set => SetValue(SeriesProperty, value);
    }

    public EnumOption<ValuationMetric>? MetricOption
    {
        get => (EnumOption<ValuationMetric>?)GetValue(MetricOptionProperty);
        set => SetValue(MetricOptionProperty, value);
    }

    public EnumOption<LookbackWindow>? WindowOption
    {
        get => (EnumOption<LookbackWindow>?)GetValue(WindowOptionProperty);
        set => SetValue(WindowOptionProperty, value);
    }

    private static void OnInputChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ValuationHistoryChart chart)
        {
            chart.Redraw();
        }
    }

    private void Redraw()
    {
        Plot plot = PlotControl.Plot;
        plot.Clear();
        ChartStyler.ApplyDarkTheme(plot);

        ValuationSeries? series = Series;
        ValuationMetric metric = MetricOption?.Value ?? ValuationMetric.PeTtm;
        LookbackWindow window = WindowOption?.Value ?? LookbackWindow.TenYears;

        if (series is null || series.Points.Count == 0)
        {
            plot.Title("暂无数据");
            ChartStyler.ApplyChineseFont(plot);
            PlotControl.Refresh();
            return;
        }

        DateTime end = series.Points[^1].Date;
        DateTime start = end.AddYears(-window.Years());

        var xs = new List<DateTime>();
        var ys = new List<double>();

        foreach (ValuationPoint point in series.Points)
        {
            if (point.Date < start || point.Date > end)
            {
                continue;
            }

            if (point.GetMetric(metric) is { } value && value > 0 && !double.IsNaN(value) && !double.IsInfinity(value))
            {
                xs.Add(point.Date);
                ys.Add(value);
            }
        }

        if (xs.Count == 0)
        {
            plot.Title("窗口内无有效样本（可能长期亏损或缺少财报）");
            ChartStyler.ApplyChineseFont(plot);
            PlotControl.Refresh();
            return;
        }

        var line = plot.Add.Scatter(xs.ToArray(), ys.ToArray());
        line.MarkerSize = 0;
        line.LineWidth = 2f;
        line.Color = ChartStyler.Price;
        line.LegendText = metric == ValuationMetric.PeTtm ? "PE(TTM)" : "PB";

        var sorted = ys.ToList();
        sorted.Sort();

        for (int i = 0; i < ReferenceQuantiles.Length; i++)
        {
            double quantile = ReferenceQuantiles[i];
            double value = PercentileCalculator.Quantile(sorted, quantile);

            var hLine = plot.Add.HorizontalLine(value);
            hLine.Color = ChartStyler.BandColor(i, ReferenceQuantiles.Length);
            hLine.LineWidth = 1.2f;
            hLine.LinePattern = LinePattern.Dashed;
            hLine.LegendText = $"{quantile * 100:0.#}% 分位：{value:N2}";
        }

        plot.Axes.DateTimeTicksBottom();
        plot.YLabel(metric == ValuationMetric.PeTtm ? "PE(TTM)" : "PB");
        plot.Title($"{(metric == ValuationMetric.PeTtm ? "市盈率" : "市净率")}历史走势 · {window.ToDisplayName()}");
        plot.ShowLegend(Alignment.UpperLeft);
        plot.Axes.AutoScale();
        ChartStyler.ApplyChineseFont(plot);

        PlotControl.Refresh();
    }
}
