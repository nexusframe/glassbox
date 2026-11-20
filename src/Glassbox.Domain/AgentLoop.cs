namespace Glassbox.Domain;

public sealed record RunLimits(int MaxTurns, int MaxToolCallsPerTurn, TimeSpan MaxRunTime);

public enum RunOutcome { Answered, MaxTurns, Timeout, BudgetExceeded, Cancelled, Error }

public sealed record RunResult(RunOutcome Outcome, string? Answer, int Turns,
    int InputTokens, int OutputTokens, decimal CostUsd);

public sealed class AgentLoop(IComplete model, ICallTool tools, IBudget budget,
    IPriceTable prices, RunLimits limits, TimeProvider time)
{
    private const int MaxResultChars = 500;
    private const string TooMany = "Too many tool calls in one turn.";

    private sealed class State(string runId, ITraceSink sink, long start)
    {
        public string RunId { get; } = runId;
        public ITraceSink Sink { get; } = sink;
        public long Start { get; } = start;
        public string? Answer;
        public int Turns, InputTokens, OutputTokens;
        public decimal CostUsd;
    }

    // Appends the user message, assistant messages and tool results to history.
    public async Task<RunResult> RunAsync(string runId, List<AgentMessage> history,
        string userText, ITraceSink sink, CancellationToken ct)
    {
        var s = new State(runId, sink, time.GetTimestamp());
        using var timeout = new CancellationTokenSource(limits.MaxRunTime, time);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, timeout.Token);
        await Emit(s, new TraceEvent(TraceType.Run, "run", runId, 0) { Phase = TracePhase.Start });
        history.Add(new AgentMessage(Role.User, userText));

        RunOutcome outcome;
        try { outcome = await LoopAsync(s, history, linked.Token); }
        catch (OperationCanceledException)
        {
            outcome = ct.IsCancellationRequested ? RunOutcome.Cancelled
                : timeout.IsCancellationRequested ? RunOutcome.Timeout : RunOutcome.Error;
        }
        catch (Exception) { outcome = RunOutcome.Error; }

        var status = outcome switch
        {
            RunOutcome.Answered => TraceStatus.Ok,
            RunOutcome.BudgetExceeded => TraceStatus.Blocked,
            _ => TraceStatus.Error,
        };
        await Emit(s, new TraceEvent(TraceType.Run, "run", runId, 0)
        {
            Phase = TracePhase.End, EndMs = Now(s), Status = status,
            InputTokens = s.InputTokens, OutputTokens = s.OutputTokens, CostUsd = s.CostUsd,
        });
        return new RunResult(outcome, outcome == RunOutcome.Answered ? s.Answer : null,
            s.Turns, s.InputTokens, s.OutputTokens, s.CostUsd);
    }

    private async Task<RunOutcome> LoopAsync(State s, List<AgentMessage> history, CancellationToken ct)
    {
        if (!await GuardAsync(s, ct)) return RunOutcome.BudgetExceeded;
        var specs = await tools.ListToolsAsync(ct);
        for (var turn = 1; turn <= limits.MaxTurns; turn++)
        {
            ct.ThrowIfCancellationRequested();
            var response = await ModelTurnAsync(s, turn, history, specs, ct);
            history.Add(new AgentMessage(Role.Assistant, response.Text,
                response.ToolCalls.Count > 0 ? response.ToolCalls : null));
            if (response.ToolCalls.Count == 0) { s.Answer = response.Text; return RunOutcome.Answered; }
            var turnStart = history.Count;
            try { await ToolBatchAsync(s, turn, response.ToolCalls, history, ct); }
            catch
            {
                // Keep history valid for the next request: every tool call needs a result.
                // Search only this turn's results: some providers reuse call ids across turns.
                var answered = history.Skip(turnStart).Select(m => m.ToolResult?.CallId).ToHashSet();
                foreach (var c in response.ToolCalls.Where(c => !answered.Contains(c.Id)))
                    history.Add(new AgentMessage(Role.Tool, null, null, new ToolResult(c.Id, "Cancelled.", true)));
                throw;
            }
            if (!await budget.CanSpendAsync(ct)) return RunOutcome.BudgetExceeded;
        }
        return RunOutcome.MaxTurns;
    }

    private async Task<bool> GuardAsync(State s, CancellationToken ct)
    {
        var start = Now(s);
        await Emit(s, new TraceEvent(TraceType.Guard, "guard", s.RunId, start)
            { Phase = TracePhase.Start, ParentId = "run" });
        var ok = await budget.CanSpendAsync(ct);
        await Emit(s, new TraceEvent(TraceType.Guard, "guard", s.RunId, start)
        {
            Phase = TracePhase.End, ParentId = "run", EndMs = Now(s),
            Status = ok ? TraceStatus.Ok : TraceStatus.Blocked,
            BudgetUsed = await budget.UsedFractionAsync(ct),
        });
        return ok;
    }

    private async Task<ModelResponse> ModelTurnAsync(State s, int turn, List<AgentMessage> history,
        IReadOnlyList<ToolSpec> specs, CancellationToken ct)
    {
        var id = $"m{turn}";
        var start = Now(s);
        await Emit(s, new TraceEvent(TraceType.Model, id, s.RunId, start)
            { Phase = TracePhase.Start, ParentId = "run", Turn = turn });
        s.Turns = turn;
        ModelResponse response;
        try
        {
            response = await model.CompleteAsync(history, specs, delta => Emit(s,
                new TraceEvent(TraceType.AnswerDelta, id + ".d", s.RunId, Now(s))
                    { ParentId = id, Turn = turn, Text = delta }), ct);
        }
        catch
        {
            await Emit(s, new TraceEvent(TraceType.Model, id, s.RunId, start)
                { Phase = TracePhase.End, ParentId = "run", Turn = turn, EndMs = Now(s), Status = TraceStatus.Error });
            throw;
        }
        var cost = prices.CostUsd(response.ModelId, response.Usage);
        await budget.AddAsync(cost, CancellationToken.None);
        s.InputTokens += response.Usage.InputTokens;
        s.OutputTokens += response.Usage.OutputTokens;
        s.CostUsd += cost;
        await Emit(s, new TraceEvent(TraceType.Model, id, s.RunId, start)
        {
            Phase = TracePhase.End, ParentId = "run", Turn = turn, EndMs = Now(s), Status = TraceStatus.Ok,
            Name = response.ModelId, InputTokens = response.Usage.InputTokens,
            OutputTokens = response.Usage.OutputTokens, CostUsd = cost,
        });
        return response;
    }

    private async Task ToolBatchAsync(State s, int turn, IReadOnlyList<ToolCall> calls,
        List<AgentMessage> history, CancellationToken ct)
    {
        var taken = calls.Take(limits.MaxToolCallsPerTurn).ToList();
        var running = taken.Select((c, i) => ToolAsync(s, turn, i + 1, c, ct)).ToList();
        var results = await Task.WhenAll(running);
        foreach (var r in results) history.Add(new AgentMessage(Role.Tool, null, null, r));
        foreach (var c in calls.Skip(taken.Count))
            history.Add(new AgentMessage(Role.Tool, null, null, new ToolResult(c.Id, TooMany, true)));
    }

    private async Task<ToolResult> ToolAsync(State s, int turn, int n, ToolCall call, CancellationToken ct)
    {
        var id = $"t{turn}.{n}";
        var start = Now(s);
        await Emit(s, new TraceEvent(TraceType.Tool, id, s.RunId, start)
            { Phase = TracePhase.Start, ParentId = $"m{turn}", Turn = turn, Name = call.Name, Args = call.ArgumentsJson });
        ToolResult result;
        try { result = await tools.CallAsync(call, ct); }
        catch (Exception)
        {
            result = new ToolResult(call.Id, "Tool failed.", true);
            if (ct.IsCancellationRequested)
            {
                await EmitToolEnd(s, id, turn, start, call.Name, result);
                throw;
            }
        }
        await EmitToolEnd(s, id, turn, start, call.Name, result);
        return result;
    }

    private ValueTask EmitToolEnd(State s, string id, int turn, int start, string name, ToolResult r) =>
        Emit(s, new TraceEvent(TraceType.Tool, id, s.RunId, start)
        {
            Phase = TracePhase.End, ParentId = $"m{turn}", Turn = turn, EndMs = Now(s), Name = name,
            Status = r.IsError ? TraceStatus.Error : TraceStatus.Ok,
            Text = Clip(r.Content),
        });

    private static string Clip(string text) =>
        text.Length <= MaxResultChars ? text : text[..MaxResultChars] + "…";

    private int Now(State s) => (int)time.GetElapsedTime(s.Start).TotalMilliseconds;

    private static ValueTask Emit(State s, TraceEvent e) => s.Sink.EmitAsync(e, CancellationToken.None);
}
