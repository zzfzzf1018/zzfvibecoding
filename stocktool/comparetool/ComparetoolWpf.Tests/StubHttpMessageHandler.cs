using System.Net;
using System.Net.Http;
using System.Text;

namespace ComparetoolWpf.Tests;

/// <summary>
/// 测试用 <see cref="HttpMessageHandler"/>：根据 URL 子串返回预设响应；
/// 也可注入抛异常的"瞬时故障"或按调用次数变更行为。
/// </summary>
internal class StubHttpMessageHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, int, HttpResponseMessage> _impl;
    private int _calls;
    public int CallCount => _calls;
    public List<string> RequestedUrls { get; } = new();

    public StubHttpMessageHandler(Func<HttpRequestMessage, int, HttpResponseMessage> impl)
    {
        _impl = impl;
    }

    public StubHttpMessageHandler(string body, string contentType = "application/json")
        : this((_, _) => Json(body, HttpStatusCode.OK, contentType)) { }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var n = Interlocked.Increment(ref _calls);
        RequestedUrls.Add(request.RequestUri?.ToString() ?? "");
        return Task.FromResult(_impl(request, n));
    }

    public static HttpResponseMessage Json(string body, HttpStatusCode status = HttpStatusCode.OK,
        string contentType = "application/json")
    {
        var resp = new HttpResponseMessage(status)
        {
            Content = new StringContent(body, Encoding.UTF8, contentType),
        };
        return resp;
    }

    public static HttpResponseMessage Bytes(byte[] data, string contentType = "text/plain")
    {
        var resp = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent(data),
        };
        resp.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(contentType);
        return resp;
    }
}
