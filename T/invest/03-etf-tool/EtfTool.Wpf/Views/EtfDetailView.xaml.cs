
using System.Windows.Controls;
using EtfTool.Data.Services;
using EtfTool.Wpf.ViewModels;

namespace EtfTool.Wpf.Views
{
    public partial class EtfDetailView : UserControl
    {
        public EtfDetailView(string etfCode, EtfService etfService)
        {
            DataContext = new EtfDetailViewModel(etfService);
            InitializeComponent();
            if (DataContext is EtfDetailViewModel viewModel)
            {
                viewModel.LoadDataAsync(etfCode);
            }
        }
    }
}
