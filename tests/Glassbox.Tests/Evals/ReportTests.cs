using Glassbox.Domain;
using Glassbox.Evals;

namespace Glassbox.Tests.Evals;

public class ReportTests
{
    private static readonly EvalCase C11 = new("C11", "Ile to 2+2?", ["calculate"], [], false, [new(@"\b4\b")], []);

    private static CaseResult Result(params (string[] Reasons, decimal Cost)[] runs) => new(C11,
        [.. runs.Select(r => new RunRecord(RunOutcome.Answered, "4", ["calculate"], r.Cost))],
        [.. runs.Select(r => new Grade(r.Reasons))]);

    [Fact]
    public void Summarises_metrics_and_reasons()
    {
        var md = Report.Markdown("m/x", new DateOnly(2026, 11, 23), 2,
            [Result(([], 0.0012m), (["from memory: calculate"], 0.0010m))]);
        Assert.Contains("Model: m/x. Date: 2026-11-23. Runs per case: 2. Cost: 0.0022 USD.", md);
        Assert.Contains("| Pass rate | 1/2 (50%) |", md);
        Assert.Contains("| Tool choice | 1/2 (50%) |", md);
        Assert.Contains("| Facts present | 2/2 (100%) |", md);
        Assert.Contains("| Stable cases | 0/1 |", md);
        Assert.Contains("| C11 | Ile to 2+2? | 1/2 | from memory: calculate (1) |", md);
    }

    [Fact]
    public void Escapes_pipes_in_cells()
    {
        var md = Report.Markdown("m/x", new DateOnly(2026, 11, 23), 1, [Result((["missing fact: a | b"], 0m))]);
        Assert.Contains(@"missing fact: a \| b (1)", md);
    }

    [Fact]
    public void Lists_failed_answers_with_run_number()
    {
        var c = new CaseResult(C11,
            [new RunRecord(RunOutcome.Answered, "4", ["calculate"], 0m),
             new RunRecord(RunOutcome.Answered, "Cztery.\nTo proste.", [], 0m),
             new RunRecord(RunOutcome.Timeout, null, [], 0m)],
            [new Grade([]), new Grade(["from memory: calculate"]), new Grade(["run: Timeout"])]);
        var md = Report.Markdown("m/x", new DateOnly(2026, 11, 23), 3, [c]);
        Assert.Contains("### Failed answers", md);
        Assert.Contains("- C11 run 2: Cztery. To proste.", md);
        Assert.Contains("- C11 run 3: (no answer)", md);
        Assert.DoesNotContain("C11 run 1:", md);
    }

    [Fact]
    public void Omits_failed_answers_when_all_pass() =>
        Assert.DoesNotContain("### Failed answers",
            Report.Markdown("m/x", new DateOnly(2026, 11, 23), 1, [Result(([], 0m))]));
}
