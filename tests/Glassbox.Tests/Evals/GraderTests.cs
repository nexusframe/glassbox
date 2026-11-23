using Glassbox.Domain;
using Glassbox.Evals;

namespace Glassbox.Tests.Evals;

public class GraderTests
{
    private static EvalCase Case(string[]? req = null, string[]? forb = null, bool noTools = false, params Fact[] facts) =>
        new("T1", "q", req ?? [], forb ?? [], noTools, facts, []);

    private static RunRecord Run(string? answer, params string[] tools) => new(RunOutcome.Answered, answer, tools, 0m);

    [Fact]
    public void Passes_when_tools_and_facts_match() =>
        Assert.True(Grader.Grade(Case(["calculate"], facts: new Fact(@"40[,.]8")), Run("To 40,8.", "calculate")).Pass);

    [Fact]
    public void Missing_required_tool_is_from_memory()
    {
        var g = Grader.Grade(Case(["calculate"], facts: new Fact(@"40[,.]8")), Run("40.8"));
        Assert.Equal(["from memory: calculate"], g.Reasons);
        Assert.False(g.ToolsOk);
        Assert.True(g.FactsOk);
    }

    [Fact]
    public void Forbidden_tool_is_reported_once() =>
        Assert.Equal(["forbidden: convert_currency"],
            Grader.Grade(Case(forb: ["convert_currency"]), Run("30", "convert_currency", "convert_currency")).Reasons);

    [Fact]
    public void No_tools_case_lists_distinct_tools() =>
        Assert.Equal(["no tools expected: get_weather, calculate"],
            Grader.Grade(Case(noTools: true), Run("x", "get_weather", "calculate", "get_weather")).Reasons);

    [Fact]
    public void Required_tool_called_twice_counts_once() =>
        Assert.True(Grader.Grade(Case(["get_weather"]), Run("ok", "get_weather", "get_weather")).Pass);

    [Fact]
    public void Missing_fact_lists_patterns()
    {
        var g = Grader.Grade(Case(facts: new Fact("Paryż", "Paris")), Run("Lyon"));
        Assert.Equal(["missing fact: Paryż | Paris"], g.Reasons);
        Assert.False(g.FactsOk);
        Assert.True(g.ToolsOk);
    }

    [Fact]
    public void Fact_match_ignores_case() =>
        Assert.True(Grader.Grade(Case(facts: new Fact("nobel")), Run("Nagroda Nobla, NOBEL")).Pass);

    [Fact]
    public void Run_without_answer_fails_and_skips_facts() =>
        Assert.Equal(["run: Timeout"],
            Grader.Grade(Case(facts: new Fact("x")), new RunRecord(RunOutcome.Timeout, null, [], 0m)).Reasons);
}
