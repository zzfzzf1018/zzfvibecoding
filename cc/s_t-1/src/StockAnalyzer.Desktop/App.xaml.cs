using System.IO;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using StockAnalyzer.Core.Services;
using StockAnalyzer.Data;
using StockAnalyzer.DataSource;
using StockAnalyzer.Desktop.ViewModels;

namespace StockAnalyzer.Desktop;

public partial class App : Application
{
    private IHost? _host;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnDomainUnhandledException;

        try
        {
            _host = BuildHost();
            await _host.StartAsync();

            var mainWindow = _host.Services.GetRequiredService<MainWindow>();
            var viewModel = _host.Services.GetRequiredService<MainViewModel>();
            mainWindow.DataContext = viewModel;

            MainWindow = mainWindow;
            mainWindow.Show();

            await viewModel.InitializeAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"应用启动失败：\n{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(-1);
        }
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        if (_host is not null)
        {
            await _host.StopAsync(TimeSpan.FromSeconds(3));
            _host.Dispose();
        }

        base.OnExit(e);
    }

    private static IHost BuildHost()
    {
        string baseDirectory = AppContext.BaseDirectory;

        HostApplicationBuilder builder = Host.CreateApplicationBuilder();

        builder.Configuration
            .SetBasePath(baseDirectory)
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
            .AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: false);

        builder.Logging.ClearProviders();
        builder.Logging.AddDebug();
        builder.Logging.AddConfiguration(builder.Configuration.GetSection("Logging"));

        string databasePath = ResolveDatabasePath(builder.Configuration, baseDirectory);

        builder.Services.AddStockDataStore(databasePath);
        builder.Services.AddEastmoneyDataSource(options =>
            builder.Configuration.GetSection(DataSourceOptions.SectionName).Bind(options));

        builder.Services.AddSingleton<StockService>();
        builder.Services.AddSingleton<MainViewModel>();
        builder.Services.AddSingleton<StockDetailViewModel>();
        builder.Services.AddSingleton<MainWindow>();

        return builder.Build();
    }

    /// <summary>解析数据库路径：配置优先，其次落在 %LOCALAPPDATA%\StockAnalyzer 下。</summary>
    private static string ResolveDatabasePath(IConfiguration configuration, string baseDirectory)
    {
        string? configured = configuration["Storage:DatabasePath"];

        if (!string.IsNullOrWhiteSpace(configured))
        {
            return Path.IsPathRooted(configured)
                ? configured
                : Path.GetFullPath(Path.Combine(baseDirectory, configured));
        }

        string folder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "StockAnalyzer");

        return Path.Combine(folder, "stock.db");
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        MessageBox.Show($"发生未处理异常：\n{e.Exception.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        e.Handled = true;
    }

    private void OnDomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception exception)
        {
            MessageBox.Show($"发生致命异常：\n{exception.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
