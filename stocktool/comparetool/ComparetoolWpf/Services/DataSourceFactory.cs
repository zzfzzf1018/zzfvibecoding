namespace ComparetoolWpf.Services;

/// <summary>
/// 数据源工厂 + 进程级单例。
/// <see cref="StockDataService"/> 和 <see cref="ScreenerService"/> 通过 <see cref="Current"/> 取实例；
/// 用户在设置界面修改后调用 <see cref="RefreshFromSettings"/> 重新装配。
/// </summary>
public static class DataSourceFactory
{
    private static IStockDataSource _current = Build(AppSettings.Current);

    /// <summary>当前生效的数据源（已装配重试 + 降级）。</summary>
    public static IStockDataSource Current => _current;

    /// <summary>当用户切换设置后调用，按新策略重建。</summary>
    public static void RefreshFromSettings()
    {
        _current = Build(AppSettings.Current);
        Logger.Info($"数据源已切换：{_current.Name}");
    }

    private static IStockDataSource Build(AppSettings s)
    {
        IStockDataSource em = new EastMoneyService();
        IStockDataSource xq = new XueqiuStockSource();
        IStockDataSource sina = new SinaStockSource();

        IStockDataSource raw = s.DataSource switch
        {
            DataSourceMode.EastMoney => em,
            DataSourceMode.Xueqiu => xq,
            DataSourceMode.Sina => sina,
            _ => new CompositeStockSource(em, xq, sina),  // Auto
        };
        return new RetryingStockSource(raw, s.RetryAttempts, s.RetryBaseDelayMs);
    }
}
