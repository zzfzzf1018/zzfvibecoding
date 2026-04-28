namespace ComparetoolWpf.Models;

/// <summary>
/// A 股股票基本信息。
/// </summary>
/// <param name="Code">不带市场前缀的 6 位代码，如 <c>600000</c>。</param>
/// <param name="Name">股票中文名称。</param>
/// <param name="Market">市场标识：<c>SH</c>（上交所）/ <c>SZ</c>（深交所）/ <c>BJ</c>（北交所）。</param>
public record StockInfo(string Code, string Name, string Market)
{
    /// <summary>带市场前缀的全代码，如 <c>SH600000</c>。东方财富部分接口需要。</summary>
    public string FullCode => $"{Market}{Code}";

    /// <summary>带市场后缀的 SECUCODE，如 <c>600000.SH</c>。</summary>
    public string SecuCode => $"{Code}.{Market}";

    public override string ToString() => $"{Code} {Name}";
}
