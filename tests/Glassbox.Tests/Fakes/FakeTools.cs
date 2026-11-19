using Glassbox.Domain;

namespace Glassbox.Tests.Fakes;

public sealed class FakeTools : ICallTool
{
    private readonly Dictionary<string, Func<ToolCall, CancellationToken, Task<ToolResult>>> _handlers = new();
    public int ListCalls { get; private set; }
    public List<ToolCall> Executed { get; } = [];

    public FakeTools On(string name, Func<ToolCall, CancellationToken, Task<ToolResult>> handler)
    {
        _handlers[name] = handler;
        return this;
    }

    public FakeTools Returns(string name, string content = "ok") =>
        On(name, (c, _) => Task.FromResult(new ToolResult(c.Id, content, false)));

    public Task<IReadOnlyList<ToolSpec>> ListToolsAsync(CancellationToken ct)
    {
        ListCalls++;
        return Task.FromResult<IReadOnlyList<ToolSpec>>(
            [.. _handlers.Keys.Select(n => new ToolSpec(n, n, "{}"))]);
    }

    public async Task<ToolResult> CallAsync(ToolCall call, CancellationToken ct)
    {
        lock (Executed) Executed.Add(call);
        if (!_handlers.TryGetValue(call.Name, out var h))
            return new ToolResult(call.Id, "Unknown tool.", true);
        return await h(call, ct);
    }
}

/// <summary>Releases all waiters once <paramref name="parties"/> have arrived; fails after a real-time bound.</summary>
public sealed class TimedBarrier(int parties)
{
    private readonly TaskCompletionSource _all = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int _arrived;

    public Task ArriveAsync()
    {
        if (Interlocked.Increment(ref _arrived) == parties) _all.TrySetResult();
        return _all.Task.WaitAsync(TimeSpan.FromSeconds(5));
    }
}
