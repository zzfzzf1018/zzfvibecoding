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
        // 其它并发测试可能正在写同一日志文件，使用共享读
        string content;
        using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
        using (var sr = new StreamReader(fs))
            content = sr.ReadToEnd();
        Assert.Contains("INFO", content);
        Assert.Contains("WARN", content);
        Assert.Contains("ERROR", content);
        Assert.Contains("InvalidOperationException", content);
    }
}
