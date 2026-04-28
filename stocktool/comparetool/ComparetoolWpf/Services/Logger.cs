using System.IO;

namespace ComparetoolWpf.Services;

/// <summary>
/// 简易文件日志器：按天分文件写到
/// <c>%LocalAppData%\ComparetoolWpf\logs\yyyy-MM-dd.log</c>。
/// 进程级单例，线程安全（lock + AppendAllText）。
/// </summary>
public static class Logger
{
    private static readonly object _lock = new();
    private static readonly string _dir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "ComparetoolWpf", "logs");

    static Logger() => Directory.CreateDirectory(_dir);

    public static string LogDirectory => _dir;

    public static void Info(string msg) => Write("INFO ", msg);
    public static void Warn(string msg) => Write("WARN ", msg);
    public static void Error(string msg, Exception? ex = null)
        => Write("ERROR", ex == null ? msg : $"{msg}\n{ex}");

    private static void Write(string level, string msg)
    {
        try
        {
            var path = Path.Combine(_dir, DateTime.Today.ToString("yyyy-MM-dd") + ".log");
            var line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [{level}] {msg}{Environment.NewLine}";
            lock (_lock) File.AppendAllText(path, line);
        }
        catch { /* 日志失败不抛出 */ }
    }
}
