using ScottPlot;
using ScottPlot.Plottables;
using ScottPlot.WinForms;

namespace StockTechAnalyzer.UI;

/// <summary>
/// 画图工具：附加到一个 FormsPlot 上，支持 趋势线 / 水平线 / 斐波那契回撤 / 清除。
/// </summary>
internal sealed class DrawingTool
{
    public enum Mode { None, Trendline, Horizontal, Fibonacci }

    private readonly FormsPlot _fp;
    private Mode _mode = Mode.None;
    private Coordinates? _firstPoint;
    private readonly List<IPlottable> _drawn = new();
    private readonly List<IPlottable> _preview = new();

    public DrawingTool(FormsPlot fp)
    {
        _fp = fp;
        _fp.MouseDown += OnDown;
        _fp.MouseMove += OnMove;
    }

    public void SetMode(Mode m)
    {
        _mode = m;
        _firstPoint = null;
        ClearPreview();
        _fp.Cursor = m == Mode.None ? Cursors.Default : Cursors.Cross;

        // 主图已经全程禁用了 ScottPlot 默认的鼠标交互，这里无需再切换。
    }

    public void ClearAll()
    {
        ClearPreview();
        foreach (var pl in _drawn) _fp.Plot.Remove(pl);
        _drawn.Clear();
        _fp.Refresh();
    }

    private void ClearPreview()
    {
        foreach (var pl in _preview) _fp.Plot.Remove(pl);
        _preview.Clear();
    }

    private void OnDown(object? sender, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left || _mode == Mode.None) return;

        // 阻止 ScottPlot 同时处理拖拽
        var c = _fp.Plot.GetCoordinates(new Pixel(e.X, e.Y));

        if (_mode == Mode.Horizontal)
        {
            var hl = _fp.Plot.Add.HorizontalLine(c.Y);
            hl.Color = ScottPlot.Color.FromHex("#FF6B6B");
            hl.LineWidth = 1.5f;
            hl.LinePattern = LinePattern.Dashed;
            hl.Text = $"{c.Y:F2}";
            _drawn.Add(hl);
            _fp.Refresh();
            return;
        }

        if (_firstPoint == null)
        {
            _firstPoint = c;
            return;
        }

        // 第二点 → 完成
        var p1 = _firstPoint.Value;
        var p2 = c;
        ClearPreview();

        switch (_mode)
        {
            case Mode.Trendline:
                AddSegment(p1, p2, ScottPlot.Color.FromHex("#4DABF7"), 2f, LinePattern.Solid);
                break;
            case Mode.Fibonacci:
                AddFibonacci(p1, p2);
                break;
        }
        _firstPoint = null;
        _fp.Refresh();
    }

    private void OnMove(object? sender, MouseEventArgs e)
    {
        if (_mode == Mode.None || _firstPoint == null) return;
        var c = _fp.Plot.GetCoordinates(new Pixel(e.X, e.Y));
        ClearPreview();
        switch (_mode)
        {
            case Mode.Trendline:
                AddSegment(_firstPoint.Value, c, ScottPlot.Color.FromHex("#4DABF7").WithAlpha(0.5), 1.5f, LinePattern.Dotted, isPreview: true);
                break;
            case Mode.Fibonacci:
                AddFibonacci(_firstPoint.Value, c, isPreview: true);
                break;
        }
        _fp.Refresh();
    }

    private void AddSegment(Coordinates a, Coordinates b, ScottPlot.Color color, float width, LinePattern pattern, bool isPreview = false)
    {
        var line = _fp.Plot.Add.Line(a, b);
        line.LineColor = color;
        line.LineWidth = width;
        line.LinePattern = pattern;
        (isPreview ? _preview : _drawn).Add(line);
    }

    private static readonly double[] FibLevels = { 0.0, 0.236, 0.382, 0.5, 0.618, 0.786, 1.0 };
    private static readonly string[] FibColors = { "#868E96", "#FFA94D", "#FF922B", "#9775FA", "#5C7CFA", "#4DABF7", "#868E96" };

    private void AddFibonacci(Coordinates a, Coordinates b, bool isPreview = false)
    {
        double xMin = Math.Min(a.X, b.X);
        double xMax = Math.Max(a.X, b.X);
        double low = Math.Min(a.Y, b.Y);
        double high = Math.Max(a.Y, b.Y);
        var bag = isPreview ? _preview : _drawn;
        for (int i = 0; i < FibLevels.Length; i++)
        {
            double y = high - (high - low) * FibLevels[i];
            var seg = _fp.Plot.Add.Line(new Coordinates(xMin, y), new Coordinates(xMax, y));
            seg.LineColor = ScottPlot.Color.FromHex(FibColors[i]).WithAlpha(isPreview ? 0.5 : 0.85);
            seg.LineWidth = 1.2f;
            seg.LinePattern = i == 0 || i == FibLevels.Length - 1 ? LinePattern.Solid : LinePattern.Dashed;
            bag.Add(seg);

            var txt = _fp.Plot.Add.Text($"  {FibLevels[i] * 100:F1}%  {y:F2}", xMax, y);
            txt.LabelFontSize = 10;
            txt.LabelFontColor = ScottPlot.Color.FromHex(FibColors[i]);
            bag.Add(txt);
        }
    }
}
