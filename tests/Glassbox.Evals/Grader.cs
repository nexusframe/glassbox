using System.Text.RegularExpressions;
using Glassbox.Domain;

namespace Glassbox.Evals;

public sealed record Grade(IReadOnlyList<string> Reasons)
{
    public const string FromMemory = "from memory: ";
    public const string Forbidden = "forbidden: ";
    public const string NoToolsExpected = "no tools expected: ";
    public const string MissingFact = "missing fact: ";

    public bool Pass => Reasons.Count == 0;

    public bool ToolsOk => !Reasons.Any(r =>
        r.StartsWith(FromMemory, StringComparison.Ordinal) ||
        r.StartsWith(Forbidden, StringComparison.Ordinal) ||
        r.StartsWith(NoToolsExpected, StringComparison.Ordinal));

    public bool FactsOk => !Reasons.Any(r => r.StartsWith(MissingFact, StringComparison.Ordinal));
}

public static class Grader
{
    private const RegexOptions Options = RegexOptions.IgnoreCase | RegexOptions.CultureInvariant;

    public static Grade Grade(EvalCase c, RunRecord r)
    {
        var reasons = new List<string>();
        if (r.Outcome != RunOutcome.Answered) reasons.Add($"run: {r.Outcome}");
        var called = r.ToolsCalled.Distinct().ToList();
        reasons.AddRange(c.Required.Where(t => !called.Contains(t)).Select(t => Evals.Grade.FromMemory + t));
        reasons.AddRange(c.Forbidden.Where(called.Contains).Select(t => Evals.Grade.Forbidden + t));
        if (c.NoTools && called.Count > 0) reasons.Add(Evals.Grade.NoToolsExpected + string.Join(", ", called));
        // Facts need an answer. A run without an answer already fails above.
        if (r.Outcome == RunOutcome.Answered)
            reasons.AddRange(c.Facts
                .Where(f => !f.Patterns.Any(p => Regex.IsMatch(r.Answer ?? "", p, Options)))
                .Select(f => Evals.Grade.MissingFact + string.Join(" | ", f.Patterns)));
        return new Grade(reasons);
    }
}
