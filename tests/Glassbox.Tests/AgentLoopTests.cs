using Glassbox.Domain;
using Glassbox.Tests.Fakes;
using Microsoft.Extensions.Time.Testing;

namespace Glassbox.Tests;

public class AgentLoopTests
{
    private static readonly RunLimits Limits = new(8, 4, TimeSpan.FromSeconds(60));

    private sealed class Rig
    {
        public FakeTimeProvider Time { get; } = new();
        public ScriptedModel Model { get; }
        public FakeTools Tools { get; } = new();
        public FakeBudget Budget { get; } = new();
        public ListSink Sink { get; } = new();
        public List<AgentMessage> History { get; } = [];
        public RunLimits Limits { get; set; } = AgentLoopTests.Limits;
        public Rig() { Model = new ScriptedModel(Time); }

        public Task<RunResult> RunAsync(CancellationToken ct = default) =>
            new AgentLoop(Model, Tools, Budget, new FixedPrices(), Limits, Time)
                .RunAsync("r1", History, "question", Sink, ct);
    }

    private static ModelResponse Say(string text, int i = 10, int o = 5) =>
        new(text, [], new ModelUsage(i, o), "m");

    private static ModelResponse Calls(int i = 10, int o = 5, params (string Id, string Name)[] calls) =>
        new(null, [.. calls.Select(c => new ToolCall(c.Id, c.Name, "{}"))], new ModelUsage(i, o), "m");

    private static ModelResponse CallOne(string name = "calculate") => Calls(10, 5, ("c1", name));

    [Fact]
    public async Task Answers_without_tools()
    {
        var r = new Rig();
        r.Model.Enqueue(Say("hi"), deltas: ["h", "i"]);
        var res = await r.RunAsync();

        Assert.Equal(RunOutcome.Answered, res.Outcome);
        Assert.Equal("hi", res.Answer);
        Assert.Equal(1, res.Turns);
        var seq = r.Sink.Events.Select(e => (e.Type, e.Phase)).ToArray();
        Assert.Equal(new (TraceType, TracePhase?)[]
        {
            (TraceType.Run, TracePhase.Start), (TraceType.Guard, TracePhase.Start),
            (TraceType.Guard, TracePhase.End), (TraceType.Model, TracePhase.Start),
            (TraceType.AnswerDelta, null), (TraceType.AnswerDelta, null),
            (TraceType.Model, TracePhase.End), (TraceType.Run, TracePhase.End),
        }, seq);
        Assert.Equal("m1", r.Sink.Events.First(e => e.Type == TraceType.AnswerDelta).ParentId);
    }

    [Fact]
    public async Task Feeds_tool_results_back()
    {
        var r = new Rig();
        r.Tools.Returns("calculate", "42");
        r.Model.Enqueue(CallOne()).Enqueue(Say("42!"));
        var res = await r.RunAsync();

        Assert.Equal(RunOutcome.Answered, res.Outcome);
        var tool = r.Model.Seen[1].Single(m => m.Role == Role.Tool);
        Assert.Equal("42", tool.ToolResult!.Content);
        Assert.Equal("c1", tool.ToolResult.CallId);
        Assert.Equal([Role.User, Role.Assistant, Role.Tool, Role.Assistant], r.History.Select(m => m.Role));
        Assert.Equal(1, r.Tools.ListCalls);
        Assert.Equal("calculate", r.Model.SeenTools[1].Single().Name);
    }

    [Fact]
    public async Task Parallel_tool_calls_share_parent()
    {
        var r = new Rig();
        var barrier = new TimedBarrier(2);
        Task<ToolResult> Wait(ToolCall c, CancellationToken _) =>
            barrier.ArriveAsync().ContinueWith(_ => new ToolResult(c.Id, "ok", false));
        r.Tools.On("a", Wait).On("b", Wait);
        r.Model.Enqueue(Calls(10, 5, ("c1", "a"), ("c2", "b"))).Enqueue(Say("done"));
        var res = await r.RunAsync();

        Assert.Equal(RunOutcome.Answered, res.Outcome);
        var tools = r.Sink.Events.Where(e => e.Type == TraceType.Tool).ToList();
        Assert.All(tools, e => Assert.Equal("m1", e.ParentId));
        var firstEnd = tools.FindIndex(e => e.Phase == TracePhase.End);
        Assert.Equal(2, tools.Take(firstEnd).Count(e => e.Phase == TracePhase.Start));
    }

    [Fact]
    public async Task Unknown_tool_is_an_error_result_not_a_crash()
    {
        var r = new Rig();
        r.Model.Enqueue(CallOne("nope")).Enqueue(Say("ok"));
        var res = await r.RunAsync();

        Assert.Equal(RunOutcome.Answered, res.Outcome);
        Assert.Equal(2, res.Turns);
        var end = r.Sink.Events.Single(e => e.Type == TraceType.Tool && e.Phase == TracePhase.End);
        Assert.Equal(TraceStatus.Error, end.Status);
    }

    [Fact]
    public async Task Throwing_tool_becomes_error_result()
    {
        var r = new Rig();
        r.Tools.On("calculate", (_, _) => throw new InvalidOperationException("secret detail"));
        r.Model.Enqueue(CallOne()).Enqueue(Say("ok"));
        var res = await r.RunAsync();

        Assert.Equal(RunOutcome.Answered, res.Outcome);
        var result = r.Model.Seen[1].Single(m => m.Role == Role.Tool).ToolResult!;
        Assert.True(result.IsError);
        Assert.Equal("Tool failed.", result.Content);
        Assert.Equal("c1", result.CallId);
    }

    [Fact]
    public async Task Stops_at_max_turns()
    {
        var r = new Rig { Limits = Limits with { MaxTurns = 5 } };
        r.Tools.Returns("calculate");
        r.Model.Repeat = new Step(CallOne());
        var res = await r.RunAsync();

        Assert.Equal(RunOutcome.MaxTurns, res.Outcome);
        Assert.Equal(5, r.Sink.Events.Count(e => e.Type == TraceType.Model && e.Phase == TracePhase.Start));
        var end = r.Sink.Events.Last();
        Assert.Equal((TraceType.Run, TraceStatus.Error), (end.Type, end.Status!.Value));
    }

    [Fact]
    public async Task Caps_tool_calls_per_turn()
    {
        var r = new Rig();
        r.Tools.Returns("calculate");
        r.Model.Enqueue(Calls(10, 5, [.. Enumerable.Range(1, 6).Select(i => ($"c{i}", "calculate"))]))
            .Enqueue(Say("ok"));
        var res = await r.RunAsync();

        Assert.Equal(RunOutcome.Answered, res.Outcome);
        Assert.Equal(4, r.Tools.Executed.Count);
        var results = r.Model.Seen[1].Where(m => m.Role == Role.Tool).Select(m => m.ToolResult!).ToList();
        Assert.Equal(6, results.Count);
        Assert.Equal(2, results.Count(x => x.IsError && x.Content == "Too many tool calls in one turn."));
        Assert.Equal(["c5", "c6"], results.Where(x => x.IsError).Select(x => x.CallId));
    }

    private static TraceEvent ToolEnd(Rig r) =>
        r.Sink.Events.Single(e => e.Type == TraceType.Tool && e.Phase == TracePhase.End);

    [Fact]
    public async Task Tool_end_carries_result_text()
    {
        var r = new Rig();
        r.Tools.Returns("calculate", "40.80");
        r.Model.Enqueue(CallOne()).Enqueue(Say("done"));
        await r.RunAsync();

        var end = ToolEnd(r);
        Assert.Equal("40.80", end.Text);
        Assert.Equal(TraceStatus.Ok, end.Status);
    }

    [Fact]
    public async Task Tool_end_carries_error_result_text()
    {
        var r = new Rig();
        r.Tools.On("calculate", (c, _) => Task.FromResult(new ToolResult(c.Id, "Division by zero.", true)));
        r.Model.Enqueue(CallOne()).Enqueue(Say("done"));
        await r.RunAsync();

        var end = ToolEnd(r);
        Assert.Equal("Division by zero.", end.Text);
        Assert.Equal(TraceStatus.Error, end.Status);
    }

    [Fact]
    public async Task Tool_end_truncates_long_result_to_500_chars()
    {
        var r = new Rig();
        r.Tools.Returns("calculate", new string('x', 600));
        r.Model.Enqueue(CallOne()).Enqueue(Say("done"));
        await r.RunAsync();

        Assert.Equal(new string('x', 500) + "…", ToolEnd(r).Text);
    }

    [Fact]
    public async Task Blocked_when_budget_is_used()
    {
        var r = new Rig();
        r.Budget.CanSpend = false;
        var res = await r.RunAsync();

        Assert.Equal(RunOutcome.BudgetExceeded, res.Outcome);
        Assert.Equal(0, r.Model.Calls);
        var guard = r.Sink.Events.Single(e => e.Type == TraceType.Guard && e.Phase == TracePhase.End);
        Assert.Equal(TraceStatus.Blocked, guard.Status);
        Assert.Equal(TraceStatus.Blocked, r.Sink.Events.Last().Status);
    }

    [Fact]
    public async Task Times_out()
    {
        var r = new Rig();
        r.Model.Enqueue(Say("late"), delay: TimeSpan.FromSeconds(61));
        var run = r.RunAsync();
        for (var i = 0; i < 400 && !run.IsCompleted; i++)
        {
            r.Time.Advance(TimeSpan.FromSeconds(1));
            await Task.Delay(5);
        }
        var res = await run.WaitAsync(TimeSpan.FromSeconds(10));

        Assert.Equal(RunOutcome.Timeout, res.Outcome);
        Assert.Equal(TraceStatus.Error, r.Sink.Events.Last().Status);
    }

    [Fact]
    public async Task Cancel_after_model_call_still_counts_cost()
    {
        var r = new Rig();
        using var cts = new CancellationTokenSource();
        r.Tools.On("calculate", (c, _) => { cts.Cancel(); return Task.FromResult(new ToolResult(c.Id, "ok", false)); });
        r.Model.Enqueue(CallOne()).Enqueue(Say("never"));
        var res = await r.RunAsync(cts.Token);

        Assert.Equal(RunOutcome.Cancelled, res.Outcome);
        Assert.Equal(1, r.Model.Calls);
        Assert.Equal([0.015m], r.Budget.Added);
        Assert.Equal(TracePhase.End, r.Sink.Events.Last().Phase);
        Assert.Equal(TraceType.Run, r.Sink.Events.Last().Type);
    }

    [Fact]
    public async Task Totals_add_up()
    {
        var r = new Rig();
        r.Tools.Returns("calculate");
        r.Model.Enqueue(Calls(100, 20, ("c1", "calculate"))).Enqueue(Say("ok", 150, 30));
        var res = await r.RunAsync();

        Assert.Equal((250, 50), (res.InputTokens, res.OutputTokens));
        Assert.Equal(0.3m, res.CostUsd);
        var end = r.Sink.Events.Last();
        Assert.Equal((250, 50, 0.3m), (end.InputTokens, end.OutputTokens, end.CostUsd));
    }

    private static void AssertHistoryConsistent(List<AgentMessage> h)
    {
        var ids = h.Where(m => m.Role == Role.Assistant).SelectMany(m => m.ToolCalls ?? []).Select(c => c.Id);
        foreach (var id in ids)
            Assert.Single(h, m => m.Role == Role.Tool && m.ToolResult!.CallId == id);
        Assert.Equal(Role.Tool, h[^1].Role);
    }

    [Fact]
    public async Task Cancel_during_tools_leaves_history_consistent()
    {
        var r = new Rig();
        using var cts = new CancellationTokenSource();
        async Task<ToolResult> Hang(ToolCall c, CancellationToken ct)
        {
            await cts.CancelAsync();
            await Task.Delay(Timeout.Infinite, ct);
            return new ToolResult(c.Id, "x", false);
        }
        r.Tools.On("a", Hang).On("b", Hang);
        r.Model.Enqueue(Calls(10, 5, ("c1", "a"), ("c2", "b")));
        var res = await r.RunAsync(cts.Token).WaitAsync(TimeSpan.FromSeconds(10));

        Assert.Equal(RunOutcome.Cancelled, res.Outcome);
        AssertHistoryConsistent(r.History);
    }

    [Fact]
    public async Task Timeout_during_tools_leaves_history_consistent()
    {
        var r = new Rig();
        r.Tools.On("a", async (c, ct) =>
        {
            await Task.Delay(TimeSpan.FromSeconds(120), r.Time, ct);
            return new ToolResult(c.Id, "x", false);
        });
        r.Model.Enqueue(Calls(10, 5, ("c1", "a"), ("c2", "a")));
        var run = r.RunAsync();
        for (var i = 0; i < 400 && !run.IsCompleted; i++)
        {
            r.Time.Advance(TimeSpan.FromSeconds(1));
            await Task.Delay(5);
        }
        var res = await run.WaitAsync(TimeSpan.FromSeconds(10));

        Assert.Equal(RunOutcome.Timeout, res.Outcome);
        AssertHistoryConsistent(r.History);
    }
}
