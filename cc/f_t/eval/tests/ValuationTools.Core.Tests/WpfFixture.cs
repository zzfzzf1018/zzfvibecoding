using System.Threading;
using System.Windows;
using System.Windows.Threading;
using ValuationTools.Desktop;
using Xunit;

namespace ValuationTools.Core.Tests;

/// <summary>提供一个常驻的 STA 线程与 Application 实例，供渲染测试加载真实的 XAML 资源。</summary>
public sealed class WpfFixture : IDisposable
{
    private readonly Dispatcher _dispatcher;

    public WpfFixture()
    {
        using var ready = new ManualResetEventSlim();
        Dispatcher? dispatcher = null;

        var thread = new Thread(() =>
        {
            var app = new App();
            app.InitializeComponent();
            dispatcher = Dispatcher.CurrentDispatcher;
            ready.Set();
            Dispatcher.Run();
        })
        {
            IsBackground = true
        };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        ready.Wait();

        _dispatcher = dispatcher!;
    }

    public T Invoke<T>(Func<T> action) => _dispatcher.Invoke(action);

    public void Dispose() => _dispatcher.InvokeShutdown();
}

[CollectionDefinition("WPF")]
public sealed class WpfCollection : ICollectionFixture<WpfFixture>;
