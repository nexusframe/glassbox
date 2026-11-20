extern alias api;
using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using Glassbox.Domain;
using Glassbox.Tests.Fakes;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;

namespace Glassbox.Tests;

/// <summary>ADR 0008: prompt text must never reach the log, except in a local Development run with LogContent on.</summary>
public class ContentLoggingTests
{
    private sealed class Capture : ILoggerProvider
    {
        public ConcurrentQueue<string> Lines { get; } = new();
        public ILogger CreateLogger(string category) => new L(this);
        public void Dispose() { }
        private sealed class L(Capture c) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
            public bool IsEnabled(LogLevel level) => true;
            public void Log<TState>(LogLevel level, EventId id, TState state, Exception? e, Func<TState, Exception?, string> f) =>
                c.Lines.Enqueue(f(state, e));
        }
    }

    private sealed class Throwing : IComplete
    {
        public Task<ModelResponse> CompleteAsync(IReadOnlyList<AgentMessage> m, IReadOnlyList<ToolSpec> t,
            Func<string, ValueTask> d, CancellationToken ct) =>
            throw new InvalidOperationException("provider said: secret question was rejected");
    }

    [Theory]
    [InlineData("Production", true, false)]
    [InlineData("Development", true, true)]
    public async Task Model_failure_is_logged_message_only_in_dev(string environment, bool flag, bool expectMessage)
    {
        var log = await RunAndCollect(environment, flag, new Throwing());
        Assert.Contains("model call failed: InvalidOperationException status=0", log);
        Assert.Contains("run outcome=Error", log);
        Assert.Equal(expectMessage, log.Contains("provider said"));
    }

    private static async Task<string> RunAndCollect(string environment, bool logContent, IComplete? model = null)
    {
        var capture = new Capture();
        model ??= new ScriptedModel { Repeat = new Step(new ModelResponse("secret answer", [], new ModelUsage(1, 1), "test-model"), ["secret answer"]) };
        using var baseFactory = new ApiFactory(model, new() { ["Glassbox:LogContent"] = logContent ? "true" : "false" });
        using var f = baseFactory.WithWebHostBuilder(b =>
        {
            b.UseEnvironment(environment);
            b.ConfigureLogging(l => l.AddProvider(capture));
        });
        var body = new StringContent(JsonSerializer.Serialize(new { message = "secret question" }), Encoding.UTF8, "application/json");
        var r = await f.CreateClient().PostAsync("/chat", body);
        await r.Content.ReadAsStringAsync();
        return string.Join("\n", capture.Lines);
    }

    [Theory]
    [InlineData("Production", true)]   // flag set on the server by mistake: still nothing
    [InlineData("Production", false)]
    [InlineData("Development", false)]
    public async Task No_content_in_logs_unless_development_and_flag(string environment, bool flag)
    {
        var log = await RunAndCollect(environment, flag);
        Assert.Contains("run outcome=Answered", log);
        Assert.DoesNotContain("secret question", log);
        Assert.DoesNotContain("secret answer", log);
    }

    [Fact]
    public async Task Development_with_flag_logs_prompt_and_answer()
    {
        var log = await RunAndCollect("Development", true);
        Assert.Contains("secret question", log);
        Assert.Contains("secret answer", log);
        Assert.Contains("Content logging is ON", log);
    }
}
