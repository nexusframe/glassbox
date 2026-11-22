using System.Net;
using System.Text;

namespace Glassbox.Tests.Fakes;

// Answers HTTP requests with canned responses and records each request URI.
public sealed class StubHttpHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> respond) : HttpMessageHandler
{
    public StubHttpHandler(Func<HttpRequestMessage, HttpResponseMessage> respond)
        : this((r, _) => Task.FromResult(respond(r))) { }

    public List<Uri> Requests { get; } = [];

    public static HttpResponseMessage Json(string body, HttpStatusCode status = HttpStatusCode.OK) =>
        new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    public HttpClient Client() => new(this);

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        Requests.Add(request.RequestUri!);
        return respond(request, ct);
    }
}
