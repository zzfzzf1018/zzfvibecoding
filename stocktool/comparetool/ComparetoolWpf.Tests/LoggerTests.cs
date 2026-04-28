using System.IO;
using ComparetoolWpf.Services;
using Xunit;

namespace ComparetoolWpf.Tests;

public class LoggerTests
{
    [Fact]
    public void Info_Warn_Error_WriteToFile()
    {
        Logger.Info("hello-info-" + Guid.NewGuid());
        Logger.Warn("hello-warn-" + Guid.NewGuid());
        Logger.Error("hello-err-" + Guid.NewGuid(), new InvalidOperationException("boom"));
        Logger.Error("no-ex");

        var path = Path.Combine(Logger.LogDirectory, DateTime.Today.ToString("yyyy-MM-dd") + ".log");
        Assert.True(File.Exists(path));
        var content = File.ReadAllText(path);
        Assert.Contains("INFO", content);
        Assert.Contains("WARN", content);
        Assert.Contains("ERROR", content);
        Assert.Contains("InvalidOperationException", content);
    }
}
