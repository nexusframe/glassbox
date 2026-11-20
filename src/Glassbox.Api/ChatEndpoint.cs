using System.Text.Json;
using Glassbox.Domain;
using Microsoft.Extensions.Options;

namespace Glassbox.Api;

public sealed record ChatRequest(string? SessionId, string? Message);

public static class ChatEndpoint
{
    private const int MaxBodyBytes = 16 * 1024;

    public static bool ModelCallsOn(GlassboxOptions o, IConfiguration config) =>
        o.ModelCallsEnabled && !string.IsNullOrWhiteSpace(config[Program.KeyName]);

    public static IResult Health(IOptionsMonitor<GlassboxOptions> options, IConfiguration config) =>
        Results.Json(new { status = "ok", modelCallsEnabled = ModelCallsOn(options.CurrentValue, config) });

    public static async Task<IResult> Post(HttpContext ctx, IOptionsMonitor<GlassboxOptions> monitor,
        IConfiguration config, SessionStore sessions, IComplete model, ICallTool tools,
        IBudget budget, IPriceTable prices, TimeProvider time, ILogger<AgentLoop> log,
        IHostEnvironment env, ILoggerFactory loggers)
    {
        var o = monitor.CurrentValue;
        if (!ModelCallsOn(o, config)) return Error(503, "paused");

        ChatRequest? req;
        try
        {
            if (ctx.Features.Get<Microsoft.AspNetCore.Http.Features.IHttpMaxRequestBodySizeFeature>() is { IsReadOnly: false } f)
                f.MaxRequestBodySize = MaxBodyBytes;
            req = await ctx.Request.ReadFromJsonAsync<ChatRequest>(JsonSerializerOptions.Web, ctx.RequestAborted);
        }
        catch (Exception e) when (e is JsonException or BadHttpRequestException or InvalidOperationException)
        {
            return Error(400, "bad_request");
        }
        var message = req?.Message;
        if (string.IsNullOrWhiteSpace(message)) return Error(400, "empty_message");
        if (message.Length > o.MaxInputChars) return Error(400, "message_too_long");

        var session = sessions.GetOrCreate(req!.SessionId);
        if (!session.Lock.Wait(0)) return Error(409, "session_busy");
        try
        {
            var runId = Guid.NewGuid().ToString("N")[..12];
            var logContent = ContentLogging.Enabled(o, env);
            if (logContent)
            {
                var content = loggers.CreateLogger("Glassbox.Content");
                model = new LoggingCompleter(model, content, runId);
                tools = new LoggingToolCaller(tools, content, runId);
            }
            model = new ErrorLoggingCompleter(model, log, runId, withMessage: logContent);
            var loop = new AgentLoop(model, tools, budget, prices,
                new RunLimits(o.MaxTurns, o.MaxToolCallsPerTurn, TimeSpan.FromSeconds(o.MaxRunSeconds)), time);
            var sink = new NdjsonTraceSink(ctx.Response, session.Id);
            var started = time.GetTimestamp();
            var result = await loop.RunAsync(runId, session.History,
                message, sink, ctx.RequestAborted);
            // Metadata only: never prompt, answer, tool args or results.
            log.LogInformation("run outcome={Outcome} turns={Turns} inputTokens={In} outputTokens={Out} costUsd={Cost} durationMs={Ms}",
                result.Outcome, result.Turns, result.InputTokens, result.OutputTokens, result.CostUsd,
                (long)time.GetElapsedTime(started).TotalMilliseconds);
            return Results.Empty;
        }
        finally { session.Lock.Release(); }
    }

    public static IResult Error(int status, string code) => Results.Json(new { error = code }, statusCode: status);
}
