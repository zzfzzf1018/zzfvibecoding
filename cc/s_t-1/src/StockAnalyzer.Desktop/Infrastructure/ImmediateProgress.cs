namespace StockAnalyzer.Desktop.Infrastructure;

/// <summary>
/// 同步投递的进度回调。
/// </summary>
/// <remarks>
/// <see cref="Progress{T}"/> 通过 <c>SynchronizationContext.Post</c> 异步投递，
/// 最后一条进度可能晚于「加载完成」的状态赋值到达，导致界面停留在中间提示上。
/// 本类型的调用方都在 UI 线程上，直接同步调用即可保证顺序。
/// </remarks>
public sealed class ImmediateProgress<T> : IProgress<T>
{
    private readonly Action<T> _handler;

    public ImmediateProgress(Action<T> handler) => _handler = handler;

    public void Report(T value) => _handler(value);
}
