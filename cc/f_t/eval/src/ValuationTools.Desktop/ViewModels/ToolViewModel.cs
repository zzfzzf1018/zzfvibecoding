using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Data;
using System.Globalization;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Input;
using Microsoft.Win32;
using ValuationTools.Desktop.Infrastructure;
using ValuationTools.Desktop.Models;

namespace ValuationTools.Desktop.ViewModels;

/// <summary>所有估值工具的基类：负责输入项管理、自动重算、结果与报告导出。</summary>
public abstract class ToolViewModel : ObservableObject
{
    private readonly Dictionary<string, InputField> _fields = new(StringComparer.OrdinalIgnoreCase);
    private bool _isReady;
    private DataView? _schedule;
    private DataView? _sensitivity;
    private string? _message;
    private bool _isError;
    private string _scheduleTitle = "测算明细";
    private string _sensitivityTitle = "敏感性分析";

    protected ToolViewModel(string title, string category, string description)
    {
        Title = title;
        Category = category;
        Description = description;
        ResetCommand = new RelayCommand(ResetInputs);
        CopyCommand = new RelayCommand(CopyReport);
        ExportCommand = new RelayCommand(ExportReport);
    }

    public string Title { get; }
    public string Category { get; }
    public string Description { get; }

    /// <summary>模型的核心公式，显示在结果区顶部。</summary>
    public string Formula { get; protected set; } = string.Empty;

    public ObservableCollection<InputGroup> Groups { get; } = new();
    public ObservableCollection<ResultItem> Results { get; } = new();

    public ICommand ResetCommand { get; }
    public ICommand CopyCommand { get; }
    public ICommand ExportCommand { get; }

    public string ScheduleTitle
    {
        get => _scheduleTitle;
        protected set => SetProperty(ref _scheduleTitle, value);
    }

    public string SensitivityTitle
    {
        get => _sensitivityTitle;
        protected set => SetProperty(ref _sensitivityTitle, value);
    }

    public DataView? Schedule
    {
        get => _schedule;
        private set { _schedule = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasSchedule)); }
    }

    public DataView? Sensitivity
    {
        get => _sensitivity;
        private set { _sensitivity = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasSensitivity)); }
    }

    public bool HasSchedule => _schedule is not null;
    public bool HasSensitivity => _sensitivity is not null;

    public string? Message
    {
        get => _message;
        private set { _message = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasMessage)); }
    }

    public bool HasMessage => !string.IsNullOrWhiteSpace(_message);

    public bool IsError
    {
        get => _isError;
        private set => SetProperty(ref _isError, value);
    }

    // ---------- 输入项构建 ----------

    protected void AddGroup(string name, params InputField[] fields)
    {
        foreach (var field in fields)
        {
            _fields[field.Key] = field;
            field.PropertyChanged += OnFieldChanged;
        }
        Groups.Add(new InputGroup(name, fields));
    }

    protected static NumberField Number(string key, string label, double value, string? unit = null, string? hint = null)
        => new(key, label, value, unit, hint);

    protected static NumberField Percent(string key, string label, double percentValue, string? hint = null)
        => new(key, label, percentValue, null, hint, isPercent: true);

    protected static ChoiceField Choice(string key, string label, string[] options, int selectedIndex = 0, string? hint = null)
        => new(key, label, options, selectedIndex, hint);

    protected static ToggleField Toggle(string key, string label, bool value = false, string? hint = null)
        => new(key, label, value, hint);

    /// <summary>输入项构建完成后调用，触发首次计算。</summary>
    protected void Ready()
    {
        _isReady = true;
        Recalculate();
    }

    private void OnFieldChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_isReady) Recalculate();
    }

    // ---------- 输入读取 ----------

    protected double V(string key) => Field<NumberField>(key).Value;
    protected double R(string key) => Field<NumberField>(key).Rate;
    protected int I(string key) => Field<NumberField>(key).IntValue;
    protected bool B(string key) => Field<ToggleField>(key).Value;
    protected int Selected(string key) => Field<ChoiceField>(key).SelectedIndex;

    private T Field<T>(string key) where T : InputField
        => _fields.TryGetValue(key, out var field) && field is T typed
            ? typed
            : throw new KeyNotFoundException($"未找到输入项 “{key}”。");

    // ---------- 结果输出 ----------

    protected void AddResult(string label, string value, bool isPrimary = false, string? note = null)
        => Results.Add(new ResultItem(label, value, isPrimary, note));

    /// <summary>列名固定为 C0/C1…，真实表头放在 Caption 中，避免表头里的 . % \ 等字符破坏 WPF 绑定路径。</summary>
    protected static DataTable CreateTable(params string[] headers)
    {
        var table = new DataTable();
        for (int i = 0; i < headers.Length; i++)
            table.Columns.Add(new DataColumn($"C{i}", typeof(string)) { Caption = headers[i] });
        return table;
    }

    protected void SetSchedule(DataTable? table) => Schedule = table?.DefaultView;
    protected void SetSensitivity(DataTable? table) => Sensitivity = table?.DefaultView;

    public void Recalculate()
    {
        Results.Clear();
        Schedule = null;
        Sensitivity = null;
        Message = null;
        IsError = false;

        try
        {
            Compute();
        }
        catch (Exception ex)
        {
            Results.Clear();
            Schedule = null;
            Sensitivity = null;
            IsError = true;
            Message = ex.Message;
        }
    }

    /// <summary>由具体工具实现：读取输入、调用核心算法、填充结果。</summary>
    protected abstract void Compute();

    /// <summary>计算过程中设置提示信息（非错误）。</summary>
    protected void SetNotice(string? notice)
    {
        if (string.IsNullOrWhiteSpace(notice)) return;
        IsError = false;
        Message = notice;
    }

    private void ResetInputs()
    {
        _isReady = false;
        foreach (var group in Groups)
            foreach (var field in group.Fields)
                field.Reset();
        _isReady = true;
        Recalculate();
    }

    public string BuildReport()
    {
        var sb = new StringBuilder();
        sb.AppendLine($"【{Title}】{Description}");
        sb.AppendLine($"生成时间：{DateTime.Now:yyyy-MM-dd HH:mm}");
        if (!string.IsNullOrWhiteSpace(Formula)) sb.AppendLine($"模型：{Formula}");
        sb.AppendLine();

        sb.AppendLine("== 输入参数 ==");
        foreach (var group in Groups)
        {
            sb.AppendLine($"[{group.Name}]");
            foreach (var field in group.Fields)
            {
                string value = field switch
                {
                    NumberField n => n.IsPercent
                        ? n.Value.ToString("0.####", CultureInfo.CurrentCulture) + "%"
                        : n.Value.ToString("0.####", CultureInfo.CurrentCulture) + (n.Unit ?? string.Empty),
                    ChoiceField c => c.Options.ElementAtOrDefault(c.SelectedIndex) ?? string.Empty,
                    ToggleField t => t.Value ? "是" : "否",
                    _ => string.Empty
                };
                sb.AppendLine($"  {field.Label}：{value}");
            }
        }

        sb.AppendLine();
        sb.AppendLine("== 计算结果 ==");
        foreach (var item in Results)
            sb.AppendLine($"  {item.Label}：{item.Value}{(item.HasNote ? $"（{item.Note}）" : string.Empty)}");

        AppendTable(sb, ScheduleTitle, Schedule);
        AppendTable(sb, SensitivityTitle, Sensitivity);

        if (HasMessage)
        {
            sb.AppendLine();
            sb.AppendLine($"提示：{Message}");
        }

        sb.AppendLine();
        sb.AppendLine("本报告由估值计算工具箱生成，结果依赖于输入假设，仅供研究参考，不构成投资建议。");
        return sb.ToString();
    }

    private static void AppendTable(StringBuilder sb, string title, DataView? view)
    {
        if (view is null) return;
        sb.AppendLine();
        sb.AppendLine($"== {title} ==");
        var columns = view.Table!.Columns.Cast<DataColumn>().ToList();
        sb.AppendLine(string.Join("\t", columns.Select(c => c.Caption)));
        foreach (DataRowView row in view)
            sb.AppendLine(string.Join("\t", columns.Select(c => row[c.ColumnName]?.ToString())));
    }

    private void CopyReport()
    {
        try
        {
            Clipboard.SetText(BuildReport());
            SetNotice("报告已复制到剪贴板。");
        }
        catch (Exception ex)
        {
            IsError = true;
            Message = "复制失败：" + ex.Message;
        }
    }

    private void ExportReport()
    {
        var dialog = new SaveFileDialog
        {
            Title = "导出估值报告",
            Filter = "文本文件 (*.txt)|*.txt|所有文件 (*.*)|*.*",
            FileName = $"{SanitizeFileName(Title)}_{DateTime.Now:yyyyMMdd_HHmm}.txt"
        };
        if (dialog.ShowDialog() != true) return;

        try
        {
            File.WriteAllText(dialog.FileName, BuildReport(), Encoding.UTF8);
            SetNotice($"报告已导出到 {dialog.FileName}");
        }
        catch (Exception ex)
        {
            IsError = true;
            Message = "导出失败：" + ex.Message;
        }
    }

    // ---------- 格式化辅助 ----------

    /// <summary>工具名称含“/”等字符，直接做文件名会被当成路径分隔符。</summary>
    internal static string SanitizeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        return string.Concat(name.Select(c => invalid.Contains(c) ? '_' : c)).Trim();
    }

    protected static string Money(double value) => value.ToString("N2", CultureInfo.CurrentCulture);
    protected static string Money0(double value) => value.ToString("N0", CultureInfo.CurrentCulture);
    protected static string Pct(double rate) => rate.ToString("P2", CultureInfo.CurrentCulture);
    protected static string Pct(double? rate) => rate.HasValue ? Pct(rate.Value) : "—";
    protected static string Times(double value) => value.ToString("N2", CultureInfo.CurrentCulture) + "x";
    protected static string Num(double value, int decimals = 2) => value.ToString("N" + decimals, CultureInfo.CurrentCulture);
    protected static string Num(double? value, int decimals = 2) => value.HasValue ? Num(value.Value, decimals) : "—";
}
