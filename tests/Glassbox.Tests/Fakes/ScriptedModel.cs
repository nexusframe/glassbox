using Glassbox.Domain;

namespace Glassbox.Tests.Fakes;

public sealed record Step(ModelResponse Response, string[]? Deltas = null, TimeSpan? Delay = null);

public sealed class ScriptedModel(TimeProvider? time = null) : IComplete
{
    private readonly Queue<Step> _steps = new();
    public Step? Repeat { get; set; }
    public int Calls { get; private set; }
    public List<AgentMessage[]> Seen { get; } = [];
    public List<IReadOnlyList<ToolSpec>> SeenTools { get; } = [];

    public ScriptedModel Enqueue(ModelResponse r, string[]? deltas = null, TimeSpan? delay = null)
    {
        _steps.Enqueue(new Step(r, deltas, delay));
        return this;
    }

    public async Task<ModelResponse> CompleteAsync(IReadOnlyList<AgentMessage> messages,
        IReadOnlyList<ToolSpec> tools, Func<string, ValueTask> onTextDelta, CancellationToken ct)
    {
        Calls++;
        Seen.Add([.. messages]);
        SeenTools.Add(tools);
        var step = _steps.Count > 0 ? _steps.Dequeue() : Repeat ?? throw new InvalidOperationException("script empty");
        if (step.Delay is { } d) await Task.Delay(d, time ?? TimeProvider.System, ct);
        foreach (var t in step.Deltas ?? []) await onTextDelta(t);
        return step.Response;
    }
}
