using Glassbox.Domain;

namespace Glassbox.Tests.Fakes;

public sealed class ListSink : ITraceSink
{
    public List<TraceEvent> Events { get; } = [];

    public ValueTask EmitAsync(TraceEvent e, CancellationToken ct)
    {
        lock (Events) Events.Add(e);
        return ValueTask.CompletedTask;
    }
}
