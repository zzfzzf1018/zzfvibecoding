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
        // 申万二级板块名（节选 高频）。匹配规则为 Contains，所以 "白酒" 也会命中 "白酒Ⅱ"。
        "白酒","软饮料","乳品","调味发酵品","食品加工","休闲食品","肉制品","啤酒",
        "化学制药","中药","生物制品","医疗器械","医疗服务","医药商业",
        "半导体","消费电子","光学光电子","元件","电子化学品","其他电子",
        "计算机设备","软件开发","IT服务","互联网电商","游戏",
        "通信服务","通信设备",
        "电池","光伏设备","风电设备","电网设备","电机","其他电源设备",
        "乘用车","商用车","汽车零部件","汽车服务","摩托车及其他",
        "白色家电","黑色家电","小家电","厨卫电器","照明设备",
        "纺织制造","服装家纺",
        "化学原料","化学制品","化学纤维","塑料","橡胶","农化制品",
        "钢铁","工业金属","贵金属","小金属","能源金属",
        "煤炭开采","焦炭","石油开采","炼化及贸易","油服工程",
        "电力","燃气","环境治理","水务",
        "通用设备","专用设备","工程机械","自动化设备","轨交设备","航海装备","航空装备","航天装备","兵器兵装","地面兵装",
        "水泥","玻璃玻纤","装修建材",
        "房地产开发","房地产服务",
        "国有大型银行","股份制银行","城商行","农商行","保险","证券","多元金融",
        "公路铁路","物流","航空机场","航运港口",
        "种植业","养殖业","渔业","饲料","动物保健","林业",
        "酒店餐饮","旅游及景区","体育","教育","专业服务",
        "贸易","一般零售","专业连锁","专业市场",
        "广告营销","出版","影视院线",
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
