using System.Configuration;
using System.Data;
using System.Windows;

namespace PdfToMarkdown;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        if (e.Args.Length > 0 && e.Args[0] == "--test" && e.Args.Length > 1)
        {
            TestRunner.RunTest(e.Args[1]);
            Shutdown();
            return;
        }
        base.OnStartup(e);
    }
}

