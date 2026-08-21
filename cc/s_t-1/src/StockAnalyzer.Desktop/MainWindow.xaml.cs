using System.Windows;
using System.Windows.Controls;
using StockAnalyzer.Core.Models;
using StockAnalyzer.Desktop.ViewModels;

namespace StockAnalyzer.Desktop;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        FitToWorkArea();
    }

    /// <summary>在低分辨率屏幕上避免窗口超出可见区域。</summary>
    private void FitToWorkArea()
    {
        double maxWidth = SystemParameters.WorkArea.Width;
        double maxHeight = SystemParameters.WorkArea.Height;

        if (Width > maxWidth || Height > maxHeight)
        {
            Width = Math.Min(Width, maxWidth);
            Height = Math.Min(Height, maxHeight);
            WindowState = WindowState.Maximized;
        }
    }

    /// <summary>双击搜索结果直接加入自选。</summary>
    private void OnSearchResultDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (DataContext is not MainViewModel viewModel)
        {
            return;
        }

        if (sender is ListBox { SelectedItem: StockInfo stock } &&
            viewModel.AddToWatchlistCommand.CanExecute(stock))
        {
            viewModel.AddToWatchlistCommand.Execute(stock);
        }
    }
}
