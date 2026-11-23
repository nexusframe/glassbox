using Glassbox.Domain;

namespace Glassbox.Evals;

public sealed record CaseResult(EvalCase Case, IReadOnlyList<RunRecord> Runs, IReadOnlyList<Grade> Grades);

/// <summary>Runs one case N times on the production loop. Each run starts with only the system prompt.</summary>
public sealed class EvalRunner(IComplete model, ICallTool realTools, IPriceTable prices,
    RunLimits limits, string systemPrompt)
{
    public async Task<CaseResult> RunCaseAsync(EvalCase c, int runs, CancellationToken ct)
    {
        var records = new List<RunRecord>();
        for (var i = 1; i <= runs; i++)
        {
            var sink = new ToolNameSink();
            var loop = new AgentLoop(model, new RecordedTools(realTools, c.Recordings), new UnlimitedBudget(),
                prices, limits, TimeProvider.System);
            var history = new List<AgentMessage> { new(Role.System, systemPrompt) };
            var result = await loop.RunAsync($"{c.Id}-{i}", history, c.Input, sink, ct);
            records.Add(new RunRecord(result.Outcome, result.Answer, sink.Names, result.CostUsd));
        }
        return new CaseResult(c, records, [.. records.Select(r => Grader.Grade(c, r))]);
    }

    /// <summary>Collects tool names from the trace. Tools in one turn run in parallel, so it locks.</summary>
    private sealed class ToolNameSink : ITraceSink
    {
        private readonly List<string> _names = [];

        public IReadOnlyList<string> Names { get { lock (_names) return [.. _names]; } }

        public ValueTask EmitAsync(TraceEvent e, CancellationToken ct)
        {
            if (e is { Type: TraceType.Tool, Phase: TracePhase.Start, Name: { } name })
                lock (_names) _names.Add(name);
            return ValueTask.CompletedTask;
        }
    }

    /// <summary>Evals are a manual run with their own cost report, so the daily budget does not apply.</summary>
    private sealed class UnlimitedBudget : IBudget
    {
        public Task<bool> CanSpendAsync(CancellationToken ct) => Task.FromResult(true);
        public Task AddAsync(decimal usd, CancellationToken ct) => Task.CompletedTask;
        public Task<decimal> UsedFractionAsync(CancellationToken ct) => Task.FromResult(0m);
    }
}
