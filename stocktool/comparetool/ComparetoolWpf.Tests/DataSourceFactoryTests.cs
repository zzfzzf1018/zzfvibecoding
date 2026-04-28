using ComparetoolWpf.Services;
using Xunit;

namespace ComparetoolWpf.Tests;

[Collection("AppSettings")]
public class DataSourceFactoryTests
{
    [Theory]
    [InlineData(DataSourceMode.Auto, "自动")]
    [InlineData(DataSourceMode.EastMoney, "东方财富")]
    [InlineData(DataSourceMode.Xueqiu, "雪球")]
    [InlineData(DataSourceMode.Sina, "新浪")]
    public void RefreshFromSettings_AppliesMode(DataSourceMode mode, string contains)
    {
        var backup = AppSettings.Current;
        try
        {
            new AppSettings { DataSource = mode, RetryAttempts = 2, RetryBaseDelayMs = 50 }.Save();
            DataSourceFactory.RefreshFromSettings();
            Assert.Contains(contains, DataSourceFactory.Current.Name);
            Assert.Contains("重试", DataSourceFactory.Current.Name);
        }
        finally
        {
            backup.Save();
            DataSourceFactory.RefreshFromSettings();
        }
    }
}
