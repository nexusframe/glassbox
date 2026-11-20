using System.Text;
using Glassbox.Domain;

namespace Glassbox.Api;

/// <summary>
/// Dev-only debug logging of prompts, answers, tool arguments and results.
/// ADR 0008 forbids this content in logs; the exception is a local Development run with
/// `Glassbox:LogContent=true`. In any other environment the flag is ignored.
/// </summary>
public static class ContentLogging
{
    public static bool Enabled(GlassboxOptions o, IHostEnvironment env) => o.LogContent && env.IsDevelopment();

    public static string Describe(AgentMessage m) => m.Role switch
    {
        Role.Tool => $"tool[{m.ToolResult?.CallId}]{(m.ToolResult?.IsError == true ? " ERROR" : "")}: {m.ToolResult?.Content}",
        Role.Assistant when m.ToolCalls is { Count: > 0 } calls =>
            $"assistant: {m.Text}" + string.Concat(calls.Select(c => $"\n    call {c.Name}[{c.Id}] {c.ArgumentsJson}")),
        _ => $"{m.Role.ToString().ToLowerInvariant()}: {m.Text}",
    };
}

/// <summary>
/// Always on: logs a failed model call as metadata only (exception type and HTTP status),
/// because the loop turns the exception into outcome Error and it would vanish otherwise.
/// The message text can echo request content, so it is logged only with content logging on.
/// </summary>
public sealed class ErrorLoggingCompleter(IComplete inner, ILogger log, string runId, bool withMessage) : IComplete
{
    public async Task<ModelResponse> CompleteAsync(IReadOnlyList<AgentMessage> messages,
        IReadOnlyList<ToolSpec> tools, Func<string, ValueTask> onTextDelta, CancellationToken ct)
    {
        try { return await inner.CompleteAsync(messages, tools, onTextDelta, ct); }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception e)
        {
            var status = e is System.ClientModel.ClientResultException c ? c.Status : 0;
            if (withMessage)
                log.LogWarning("[{RunId}] model call failed: {Type} status={Status}: {Message}", runId, e.GetType().Name, status, e.Message);
            else
                log.LogWarning("[{RunId}] model call failed: {Type} status={Status}", runId, e.GetType().Name, status);
            throw;
        }
    }
}

public sealed class LoggingCompleter(IComplete inner, ILogger log, string runId) : IComplete
{
    public async Task<ModelResponse> CompleteAsync(IReadOnlyList<AgentMessage> messages,
        IReadOnlyList<ToolSpec> tools, Func<string, ValueTask> onTextDelta, CancellationToken ct)
    {
        var sent = new StringBuilder();
        foreach (var m in messages) sent.Append("\n  ").Append(ContentLogging.Describe(m));
        log.LogInformation("[{RunId}] model request ({Count} messages, {Tools} tools):{Messages}",
            runId, messages.Count, tools.Count, sent);
        var r = await inner.CompleteAsync(messages, tools, onTextDelta, ct);
        var calls = string.Concat(r.ToolCalls.Select(c => $"\n  call {c.Name}[{c.Id}] {c.ArgumentsJson}"));
        log.LogInformation("[{RunId}] model response (in={In} out={Out}): text={Text}{Calls}",
            runId, r.Usage.InputTokens, r.Usage.OutputTokens, r.Text ?? "<none>", calls);
        return r;
    }
}

public sealed class LoggingToolCaller(ICallTool inner, ILogger log, string runId) : ICallTool
{
    public Task<IReadOnlyList<ToolSpec>> ListToolsAsync(CancellationToken ct) => inner.ListToolsAsync(ct);

    public async Task<ToolResult> CallAsync(ToolCall call, CancellationToken ct)
    {
        var r = await inner.CallAsync(call, ct);
        log.LogInformation("[{RunId}] tool {Name}[{Id}] args={Args} -> {Status}: {Result}",
            runId, call.Name, call.Id, call.ArgumentsJson, r.IsError ? "ERROR" : "ok", r.Content);
        return r;
    }
}
