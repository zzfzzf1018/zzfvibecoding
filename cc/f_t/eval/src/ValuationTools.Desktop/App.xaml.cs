using System.Windows;

namespace ValuationTools.Desktop;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        DispatcherUnhandledException += (_, args) =>
        {
            MessageBox.Show(args.Exception.Message, "发生错误", MessageBoxButton.OK, MessageBoxImage.Error);
            args.Handled = true;
        };
    }
}
