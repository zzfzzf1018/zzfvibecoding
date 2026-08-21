using System.Data;
using System.Windows.Controls;

namespace ValuationTools.Desktop.Views;

public partial class ToolView : UserControl
{
    public ToolView()
    {
        InitializeComponent();
    }

    private void OnAutoGeneratingColumn(object sender, DataGridAutoGeneratingColumnEventArgs e)
    {
        if (sender is DataGrid { ItemsSource: DataView view } && view.Table!.Columns.Contains(e.PropertyName))
            e.Column.Header = view.Table.Columns[e.PropertyName]!.Caption;
    }
}
