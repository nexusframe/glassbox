using Glassbox.Domain;
using Glassbox.Evals;
using Glassbox.Tests.Fakes;

namespace Glassbox.Tests.Evals;

public class EvalRunnerTests
{
    /// <summary>Calls calculate after a user message, answers "4" after a tool result. Replays on every run.</summary>
    private sealed class CalcModel : IComplete
    {
        public List<AgentMessage[]> Seen { get; } = [];

        public Task<ModelResponse> CompleteAsync(IReadOnlyList<AgentMessage> messages,
            IReadOnlyList<ToolSpec> tools, Func<string, ValueTask> onTextDelta, CancellationToken ct)
        {
            Seen.Add([.. messages]);
            var usage = new ModelUsage(10, 5);
            return Task.FromResult(messages[^1].Role == Role.User
                ? new ModelResponse(null, [new ToolCall("c1", "calculate", """{"expression":"2+2"}""")], usage, "m")
                : new ModelResponse("4", [], usage, "m"));
        }
    }

    private sealed class FreePrices : IPriceTable
    {
        public decimal CostUsd(string modelId, ModelUsage usage) => 0.001m;
    }

    private static readonly EvalCase C11 = new("C11", "Ile to 2+2?", ["calculate"], [], false, [new(@"\b4\b")], []);

    private static EvalRunner Runner(CalcModel model) => new(model, new FakeTools().Returns("calculate", "4"),
        new FreePrices(), new RunLimits(5, 4, TimeSpan.FromSeconds(30)), "SYSTEM");

    [Fact]
    public async Task Records_tool_names_and_grades_each_run()
    {
        var r = await Runner(new CalcModel()).RunCaseAsync(C11, 2, CancellationToken.None);
        Assert.Equal(2, r.Runs.Count);
        Assert.All(r.Runs, run => Assert.Equal(["calculate"], run.ToolsCalled));
        Assert.All(r.Runs, run => Assert.Equal(0.002m, run.CostUsd));
        Assert.All(r.Grades, g => Assert.True(g.Pass));
    }

    [Fact]
    public async Task Starts_each_run_with_only_the_system_prompt()
    {
        var model = new CalcModel();
        await Runner(model).RunCaseAsync(C11, 2, CancellationToken.None);
        var firstOfRun2 = model.Seen[2];
        Assert.Equal([Role.System, Role.User], firstOfRun2.Select(m => m.Role));
        Assert.Equal("SYSTEM", firstOfRun2[0].Text);
    }
}
