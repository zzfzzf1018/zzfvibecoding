using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Data;
using ValuationTools.Desktop.Infrastructure;
using ValuationTools.Desktop.ViewModels.Tools;

namespace ValuationTools.Desktop.ViewModels;

public sealed class MainViewModel : ObservableObject
{
    private ToolViewModel? _selectedTool;
    private string _searchText = string.Empty;

    public MainViewModel()
    {
        Tools = new ObservableCollection<ToolViewModel>
        {
            new DcfViewModel(),
            new DdmViewModel(),
            new ResidualIncomeViewModel(),
            new PegViewModel(),
            new RelativeValuationViewModel(),
            new GrahamViewModel(),
            new DiscountRateViewModel(),
            new GrowthViewModel(),
            new ProjectCashFlowViewModel(),
            new BondViewModel(),
            new OptionViewModel()
        };

        ToolsView = new CollectionViewSource { Source = Tools };
        ToolsView.GroupDescriptions.Add(new PropertyGroupDescription(nameof(ToolViewModel.Category)));
        ToolsView.View.Filter = FilterTool;

        SelectedTool = Tools[0];
    }

    public ObservableCollection<ToolViewModel> Tools { get; }

    public CollectionViewSource ToolsView { get; }

    public ICollectionView FilteredTools => ToolsView.View;

    public ToolViewModel? SelectedTool
    {
        get => _selectedTool;
        set => SetProperty(ref _selectedTool, value);
    }

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (!SetProperty(ref _searchText, value)) return;
            ToolsView.View.Refresh();
            if (SelectedTool is not null && !FilterTool(SelectedTool))
                SelectedTool = ToolsView.View.Cast<ToolViewModel>().FirstOrDefault();
        }
    }

    private bool FilterTool(object item)
    {
        if (string.IsNullOrWhiteSpace(_searchText)) return true;
        if (item is not ToolViewModel tool) return false;

        return tool.Title.Contains(_searchText, StringComparison.OrdinalIgnoreCase)
               || tool.Category.Contains(_searchText, StringComparison.OrdinalIgnoreCase)
               || tool.Description.Contains(_searchText, StringComparison.OrdinalIgnoreCase);
    }
}
