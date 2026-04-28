using System.Windows;
using System.Windows.Threading;

namespace ComparetoolWpf;

/// <summary>
/// WPF 应用程序入口。
/// 通过 App.xaml 中 <c>StartupUri</c> 自动加载 <see cref="Views.MainWindow"/>。
///
/// 这里挂上全局未处理异常监听，避免启动期 ViewModel 构造失败时窗口直接消失，
/// 让用户能看到具体错误。
/// </summary>
public partial class App : Application
{
    public App()
    {
        DispatcherUnhandledException += (_, e) =>
        {
            MessageBox.Show(
                $"未处理异常：\n{e.Exception}",
                "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            e.Handled = true;
        };
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            MessageBox.Show(
                $"未处理异常：\n{e.ExceptionObject}",
                "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        };
        System.Threading.Tasks.TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            MessageBox.Show(
                $"未观察的 Task 异常：\n{e.Exception}",
                "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            e.SetObserved();
        };
    }
}
