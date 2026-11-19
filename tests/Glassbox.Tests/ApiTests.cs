extern alias api;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Glassbox.Domain;
using Glassbox.Tests.Fakes;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Glassbox.Tests;

public sealed class ApiFactory(IComplete model, Dictionary<string, string?>? settings = null)
    : WebApplicationFactory<api::Program>
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "glassbox-test-" + Guid.NewGuid().ToString("N"));

    protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
    {
        var cfg = new Dictionary<string, string?>
        {
            ["OPENROUTER_API_KEY"] = "test-key",
            ["Glassbox:ModelId"] = "test-model",
            ["Glassbox:Prices:test-model:InputPerMTok"] = "1",
            ["Glassbox:Prices:test-model:OutputPerMTok"] = "1",
            ["Glassbox:BudgetFile"] = Path.Combine(_dir, "budget.json"),
            ["Glassbox:AllowedOrigins:0"] = "https://ok.example",
        };
        foreach (var (k, v) in settings ?? []) cfg[k] = v;
        builder.ConfigureAppConfiguration((_, c) => c.AddInMemoryCollection(cfg));
        builder.ConfigureServices(s =>
        {
            s.RemoveAll<IComplete>();
            s.RemoveAll<ICallTool>();
            s.AddSingleton(model);
            s.AddSingleton<ICallTool>(new FakeTools());
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        try { if (Directory.Exists(_dir)) Directory.Delete(_dir, true); } catch { }
    }
}

public class ApiTests
{
    private static readonly TimeSpan Bound = TimeSpan.FromSeconds(10);

    private static ModelResponse Reply(string text) =>
        new(text, [], new ModelUsage(1, 1), "test-model");

    private static ScriptedModel HiModel() =>
        new() { Repeat = new Step(Reply("Hi"), ["Hi"]) };

    private static StringContent Body(string? sessionId, string message)
    {
        var json = JsonSerializer.Serialize(new { sessionId, message });
        return new StringContent(json, Encoding.UTF8, "application/json");
    }

    private static async Task<List<TraceEvent>> ReadEvents(HttpResponseMessage r)
    {
        var text = await r.Content.ReadAsStringAsync().WaitAsync(Bound);
        return [.. text.Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(l => JsonSerializer.Deserialize<TraceEvent>(l, TraceJson.Options)!)];
    }

    private static async Task<string> ErrorOf(HttpResponseMessage r)
    {
        using var doc = JsonDocument.Parse(await r.Content.ReadAsStringAsync().WaitAsync(Bound));
        return doc.RootElement.GetProperty("error").GetString()!;
    }

    [Fact]
    public async Task Health_returns_ok()
    {
        using var f = new ApiFactory(HiModel());
        var r = await f.CreateClient().GetAsync("/health");
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        using var doc = JsonDocument.Parse(await r.Content.ReadAsStringAsync());
        Assert.Equal("ok", doc.RootElement.GetProperty("status").GetString());
        Assert.True(doc.RootElement.GetProperty("modelCallsEnabled").GetBoolean());
    }

    [Fact]
    public async Task Health_reports_disabled_without_key()
    {
        using var f = new ApiFactory(HiModel(), new() { ["OPENROUTER_API_KEY"] = "" });
        var r = await f.CreateClient().GetAsync("/health");
        using var doc = JsonDocument.Parse(await r.Content.ReadAsStringAsync());
        Assert.False(doc.RootElement.GetProperty("modelCallsEnabled").GetBoolean());
    }

    [Fact]
    public async Task Chat_streams_ndjson_events()
    {
        using var f = new ApiFactory(HiModel());
        var r = await f.CreateClient().PostAsync("/chat", Body(null, "hello"));
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        Assert.Equal("application/x-ndjson", r.Content.Headers.ContentType?.MediaType);
        Assert.True(r.Headers.Contains("X-Session-Id"));
        var events = await ReadEvents(r);
        Assert.Equal(TraceType.Run, events[0].Type);
        Assert.Equal(TraceType.Run, events[^1].Type);
        Assert.Equal(TracePhase.End, events[^1].Phase);
        Assert.Contains(events, e => e.Type == TraceType.AnswerDelta && e.Text == "Hi");
    }

    [Fact]
    public async Task Second_message_keeps_history()
    {
        var model = HiModel();
        using var f = new ApiFactory(model);
        var c = f.CreateClient();
        var r1 = await c.PostAsync("/chat", Body(null, "one"));
        var sid = r1.Headers.GetValues("X-Session-Id").Single();
        await ReadEvents(r1);
        var r2 = await c.PostAsync("/chat", Body(sid, "two"));
        await ReadEvents(r2);
        Assert.Equal(sid, r2.Headers.GetValues("X-Session-Id").Single());
        var second = model.Seen[1];
        Assert.Equal(Role.System, second[0].Role);
        Assert.Equal([Role.User, Role.Assistant, Role.User], second.Skip(1).Select(m => m.Role));
    }

    [Fact]
    public async Task Unknown_session_gets_new_id()
    {
        using var f = new ApiFactory(HiModel());
        var r = await f.CreateClient().PostAsync("/chat", Body("nope", "hello"));
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        Assert.NotEqual("nope", r.Headers.GetValues("X-Session-Id").Single());
        await ReadEvents(r);
    }

    private sealed class GateModel : IComplete
    {
        public readonly TaskCompletionSource Entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public readonly TaskCompletionSource Release = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async Task<ModelResponse> CompleteAsync(IReadOnlyList<AgentMessage> messages,
            IReadOnlyList<ToolSpec> tools, Func<string, ValueTask> onTextDelta, CancellationToken ct)
        {
            Entered.TrySetResult();
            await Release.Task.WaitAsync(Bound, ct);
            return Reply("done");
        }
    }

    [Fact]
    public async Task Busy_session_returns_409()
    {
        var model = new GateModel();
        using var f = new ApiFactory(model);
        var c = f.CreateClient();
        using var req = new HttpRequestMessage(HttpMethod.Post, "/chat") { Content = Body(null, "one") };
        using var first = await c.SendAsync(req, HttpCompletionOption.ResponseHeadersRead).WaitAsync(Bound);
        var sid = first.Headers.GetValues("X-Session-Id").Single();
        await model.Entered.Task.WaitAsync(Bound);

        var second = await c.PostAsync("/chat", Body(sid, "two")).WaitAsync(Bound);
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        Assert.Equal("session_busy", await ErrorOf(second));

        model.Release.SetResult();
        var events = await ReadEvents(first);
        Assert.Equal(TracePhase.End, events[^1].Phase);
    }

    [Fact]
    public async Task Too_long_message_returns_400()
    {
        using var f = new ApiFactory(HiModel());
        var r = await f.CreateClient().PostAsync("/chat", Body(null, new string('a', 501)));
        Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode);
        Assert.Equal("message_too_long", await ErrorOf(r));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   \n\t")]
    public async Task Empty_message_returns_400(string message)
    {
        using var f = new ApiFactory(HiModel());
        var r = await f.CreateClient().PostAsync("/chat", Body(null, message));
        Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode);
        Assert.Equal("empty_message", await ErrorOf(r));
    }

    [Fact]
    public async Task Kill_switch_returns_503()
    {
        var model = HiModel();
        using var f = new ApiFactory(model, new() { ["Glassbox:ModelCallsEnabled"] = "false" });
        var r = await f.CreateClient().PostAsync("/chat", Body(null, "hello"));
        Assert.Equal(HttpStatusCode.ServiceUnavailable, r.StatusCode);
        Assert.Equal("paused", await ErrorOf(r));
        Assert.Equal(0, model.Calls);
    }

    [Fact]
    public async Task Missing_key_returns_503()
    {
        var model = HiModel();
        using var f = new ApiFactory(model, new() { ["OPENROUTER_API_KEY"] = "" });
        var r = await f.CreateClient().PostAsync("/chat", Body(null, "hello"));
        Assert.Equal(HttpStatusCode.ServiceUnavailable, r.StatusCode);
        Assert.Equal(0, model.Calls);
    }

    [Fact]
    public async Task Rate_limit_returns_429()
    {
        using var f = new ApiFactory(HiModel(), new() { ["Glassbox:RateLimitPerMinute"] = "2" });
        var c = f.CreateClient();
        for (var i = 0; i < 2; i++)
        {
            var ok = await c.PostAsync("/chat", Body(null, "hello"));
            Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
            await ReadEvents(ok);
        }
        var r = await c.PostAsync("/chat", Body(null, "hello"));
        Assert.Equal(HttpStatusCode.TooManyRequests, r.StatusCode);
        Assert.Equal("rate_limited", await ErrorOf(r));
    }

    [Fact]
    public async Task Cors_allows_only_listed_origin()
    {
        using var f = new ApiFactory(HiModel());
        var c = f.CreateClient();
        HttpRequestMessage Preflight(string origin)
        {
            var m = new HttpRequestMessage(HttpMethod.Options, "/chat");
            m.Headers.Add("Origin", origin);
            m.Headers.Add("Access-Control-Request-Method", "POST");
            m.Headers.Add("Access-Control-Request-Headers", "content-type");
            return m;
        }
        var ok = await c.SendAsync(Preflight("https://ok.example"));
        Assert.Equal("https://ok.example", ok.Headers.GetValues("Access-Control-Allow-Origin").Single());

        var real = new HttpRequestMessage(HttpMethod.Post, "/chat") { Content = Body(null, "hi") };
        real.Headers.Add("Origin", "https://ok.example");
        var resp = await c.SendAsync(real);
        Assert.Contains("X-Session-Id", string.Join(",", resp.Headers.GetValues("Access-Control-Expose-Headers")));
        await ReadEvents(resp);

        var bad = await c.SendAsync(Preflight("https://evil.example"));
        Assert.False(bad.Headers.Contains("Access-Control-Allow-Origin"));
    }

    [Fact]
    public void Missing_price_fails_startup()
    {
        using var f = new ApiFactory(HiModel(), new() { ["Glassbox:ModelId"] = "no-price-model" });
        var e = Assert.ThrowsAny<Exception>(() => f.CreateClient());
        Assert.Contains("no-price-model", e.ToString());
    }
}
