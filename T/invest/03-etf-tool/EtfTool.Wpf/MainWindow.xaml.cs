
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using EtfTool.Core.Models;
using EtfTool.Data.Cache;
using EtfTool.Data.Providers;
using EtfTool.Data.Services;
using EtfTool.Wpf.ViewModels;
using EtfTool.Wpf.Views;

namespace EtfTool.Wpf
{
    public partial class MainWindow
    {
        private readonly EtfService _etfService;

        public MainWindow()
        {
            var cacheManager = new SqliteCacheManager();
            var providerFactory = new EtfDataProviderFactory();
            _etfService = new EtfService(providerFactory, cacheManager);

            DataContext = new MainViewModel(_etfService);
            InitializeComponent();
        }

        private void ListBox_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (sender is ListBox listBox && listBox.SelectedItem is EtfInfo etf)
            {
                var detailView = new EtfDetailView(etf.Code, _etfService);
                MainFrame.Navigate(detailView);
            }
        }
    }
}
