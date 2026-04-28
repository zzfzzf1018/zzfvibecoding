using System.Windows;
using System.Windows.Threading;
using ComparetoolWpf.Services;

namespace ComparetoolWpf;

/// <summary>
/// WPF 应用程序入口。
/// 通过 App.xaml 中 <c>StartupUri</c> 自动加载 <see cref="Views.MainWindow"/>。
///
/// 这里挂上全局未处理异常监听，避免启动期 ViewModel 构造失败时窗口直接消失，
/// 让用户能看到具体错误，并把异常写入日志文件。
/// </summary>
public partial class App : Application
{
    public App()
    {
        Logger.Info($"应用启动 v{System.Reflection.Assembly.GetExecutingAssembly().GetName().Version}");
        DispatcherUnhandledException += (_, e) =>
        {
            Logger.Error("DispatcherUnhandledException", e.Exception);
            MessageBox.Show(
                $"未处理异常：\n{e.Exception.Message}\n\n详情见日志：{Logger.LogDirectory}",
                "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            e.Handled = true;
        };
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            Logger.Error("AppDomain.UnhandledException", e.ExceptionObject as Exception);
            MessageBox.Show(
                $"未处理异常：\n{e.ExceptionObject}",
                "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        };
        System.Threading.Tasks.TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            Logger.Error("UnobservedTaskException", e.Exception);
            e.SetObserved();
        };
    }
}
