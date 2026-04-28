using System.IO;
using ComparetoolWpf.Services;
using Xunit;

namespace ComparetoolWpf.Tests;

public class AppSettingsTests
{
    [Fact]
    public void Save_Load_RoundTrip()
    {
        var original = AppSettings.Current;
        try
        {
            var s = new AppSettings
            {
                DataSource = DataSourceMode.Xueqiu,
                RetryAttempts = 5,
                RetryBaseDelayMs = 250,
                HttpTimeoutSeconds = 9,
            };
            s.Save();
            Assert.Equal(DataSourceMode.Xueqiu, AppSettings.Current.DataSource);

            var reloaded = AppSettings.Load();
            Assert.Equal(5, reloaded.RetryAttempts);
            Assert.Equal(250, reloaded.RetryBaseDelayMs);
            Assert.Equal(9, reloaded.HttpTimeoutSeconds);
        }
        finally
        {
            // 还原默认，避免污染其他测试
            new AppSettings().Save();
        }
    }

    [Fact]
    public void Load_WhenFileMissing_ReturnsDefaults()
    {
        // 备份并删除后重新加载
        var path = AppSettings.SettingsPath;
        var backup = path + ".bak";
        if (File.Exists(path)) File.Move(path, backup, true);
        try
        {
            var s = AppSettings.Load();
            Assert.Equal(DataSourceMode.Auto, s.DataSource);
            Assert.True(s.RetryAttempts >= 1);
        }
        finally
        {
            if (File.Exists(backup)) File.Move(backup, path, true);
            new AppSettings().Save();
        }
    }
}
