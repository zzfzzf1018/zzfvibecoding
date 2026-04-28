using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace ComparetoolWpf.Views;

/// <summary>
/// 行业多选窗口。预置常见 A 股行业关键字，支持过滤、全选、全不选、添加自定义关键字。
/// 调用方在 <see cref="ShowDialog()"/> 返回 true 后读取 <see cref="SelectedIndustries"/>。
/// </summary>
public partial class IndustryPickerWindow : Window
{
    /// <summary>显示项，含勾选状态。</summary>
    public class Item : INotifyPropertyChanged
    {
        private bool _isChecked;
        public string Name { get; set; } = string.Empty;
        public bool IsChecked
        {
            get => _isChecked;
            set { _isChecked = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsChecked))); }
        }
        public event PropertyChangedEventHandler? PropertyChanged;
    }

    private static readonly string[] DefaultIndustries =
    {
        "银行","证券","保险","房地产","医药生物","食品饮料","白酒","电子","半导体",
        "计算机","软件服务","通信","传媒","互联网","汽车","家用电器","纺织服装","轻工制造",
        "化工","钢铁","有色金属","煤炭","石油石化","电力","公用事业","机械设备","电气设备",
        "新能源","光伏","风电","锂电池","国防军工","建筑材料","建筑装饰","商业贸易",
        "交通运输","物流","港口航运","农林牧渔","休闲服务","旅游","教育","环保","综合",
    };

    private readonly ObservableCollection<Item> _items = new();
    private readonly ICollectionView _view;

    public IndustryPickerWindow(IEnumerable<string>? preselected = null)
    {
        InitializeComponent();
        var pre = new HashSet<string>(preselected ?? Array.Empty<string>(), StringComparer.Ordinal);
        foreach (var name in DefaultIndustries.Distinct())
            _items.Add(new Item { Name = name, IsChecked = pre.Contains(name) });
        // 把不在默认列表中的预选项也补进来
        foreach (var p in pre)
            if (!_items.Any(i => i.Name == p))
                _items.Add(new Item { Name = p, IsChecked = true });
        _view = CollectionViewSource.GetDefaultView(_items);
        IndustryList.ItemsSource = _view;
    }

    /// <summary>OK 后返回所有勾选的行业关键字。</summary>
    public List<string> SelectedIndustries =>
        _items.Where(i => i.IsChecked).Select(i => i.Name).ToList();

    private void FilterBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        var text = FilterBox.Text?.Trim() ?? string.Empty;
        _view.Filter = string.IsNullOrEmpty(text) ? null
            : (Predicate<object>)(o => o is Item it && it.Name.Contains(text, StringComparison.OrdinalIgnoreCase));
    }

    private void SelectAll_Click(object sender, RoutedEventArgs e)
    {
        foreach (var it in _view.Cast<Item>()) it.IsChecked = true;
    }

    private void ClearAll_Click(object sender, RoutedEventArgs e)
    {
        foreach (var it in _items) it.IsChecked = false;
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }
}
