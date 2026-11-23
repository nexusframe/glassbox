using Glassbox.Domain;

namespace Glassbox.Evals;

/// <summary>One expected fact. The fact is present when any pattern matches the answer.</summary>
public sealed record Fact(params string[] Patterns);

/// <summary>A fixed tool result. A null pattern matches any arguments.</summary>
public sealed record Recording(string Tool, string? ArgsPattern, string Result);

/// <summary>One eval case. The first letter of the id is the group.</summary>
public sealed record EvalCase(string Id, string Input, string[] Required, string[] Forbidden,
    bool NoTools, Fact[] Facts, Recording[] Recordings);

/// <summary>The result of one run. ToolsCalled keeps the call order and the duplicates.</summary>
public sealed record RunRecord(RunOutcome Outcome, string? Answer, IReadOnlyList<string> ToolsCalled, decimal CostUsd);
