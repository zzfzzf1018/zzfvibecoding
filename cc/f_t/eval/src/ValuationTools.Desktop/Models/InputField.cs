using System.Collections.ObjectModel;
using ValuationTools.Desktop.Infrastructure;

namespace ValuationTools.Desktop.Models;

public abstract class InputField : ObservableObject
{
    protected InputField(string key, string label, string? hint)
    {
        Key = key;
        Label = label;
        Hint = hint;
    }

    public string Key { get; }
    public string Label { get; }
    public string? Hint { get; }

    public abstract void Reset();
}

/// <summary>数值输入项。IsPercent 为真时，界面输入 8 表示 8%，<see cref="Rate"/> 返回 0.08。</summary>
public sealed class NumberField : InputField
{
    private readonly double _defaultValue;
    private double _value;

    public NumberField(string key, string label, double value, string? unit = null, string? hint = null, bool isPercent = false)
        : base(key, label, hint)
    {
        _value = value;
        _defaultValue = value;
        Unit = isPercent ? "%" : unit;
        IsPercent = isPercent;
    }

    public string? Unit { get; }
    public bool IsPercent { get; }
    public bool HasUnit => !string.IsNullOrEmpty(Unit);

    public double Value
    {
        get => _value;
        set
        {
            if (SetProperty(ref _value, value))
            {
                OnPropertyChanged(nameof(Rate));
                OnPropertyChanged(nameof(IntValue));
            }
        }
    }

    /// <summary>百分比字段返回小数形式，否则等于 <see cref="Value"/>。</summary>
    public double Rate => IsPercent ? _value / 100.0 : _value;

    public int IntValue => (int)Math.Round(_value);

    public override void Reset() => Value = _defaultValue;
}

/// <summary>下拉选择项。</summary>
public sealed class ChoiceField : InputField
{
    private readonly int _defaultIndex;
    private int _selectedIndex;

    public ChoiceField(string key, string label, IEnumerable<string> options, int selectedIndex = 0, string? hint = null)
        : base(key, label, hint)
    {
        Options = new ObservableCollection<string>(options);
        _selectedIndex = selectedIndex;
        _defaultIndex = selectedIndex;
    }

    public ObservableCollection<string> Options { get; }

    public int SelectedIndex
    {
        get => _selectedIndex;
        set => SetProperty(ref _selectedIndex, value);
    }

    public override void Reset() => SelectedIndex = _defaultIndex;
}

/// <summary>开关项。</summary>
public sealed class ToggleField : InputField
{
    private readonly bool _defaultValue;
    private bool _value;

    public ToggleField(string key, string label, bool value = false, string? hint = null)
        : base(key, label, hint)
    {
        _value = value;
        _defaultValue = value;
    }

    public bool Value
    {
        get => _value;
        set => SetProperty(ref _value, value);
    }

    public override void Reset() => Value = _defaultValue;
}

public sealed class InputGroup
{
    public InputGroup(string name, IEnumerable<InputField> fields)
    {
        Name = name;
        Fields = new ObservableCollection<InputField>(fields);
    }

    public string Name { get; }
    public ObservableCollection<InputField> Fields { get; }
}

public sealed class ResultItem
{
    public ResultItem(string label, string value, bool isPrimary = false, string? note = null)
    {
        Label = label;
        Value = value;
        IsPrimary = isPrimary;
        Note = note;
    }

    public string Label { get; }
    public string Value { get; }
    public bool IsPrimary { get; }
    public string? Note { get; }
    public bool HasNote => !string.IsNullOrWhiteSpace(Note);
}
