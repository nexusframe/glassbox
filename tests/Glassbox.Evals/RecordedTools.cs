using System.Text.RegularExpressions;
using Glassbox.Domain;

namespace Glassbox.Evals;

/// <summary>
/// Gives the model the real tool specs. Runs calculate for real, because it is pure.
/// Returns recorded results for all other tools, so that expected facts stay fixed.
/// </summary>
public sealed class RecordedTools(ICallTool real, IReadOnlyList<Recording> recordings) : ICallTool
{
    private const string RealTool = "calculate";
    private const RegexOptions Options = RegexOptions.IgnoreCase | RegexOptions.CultureInvariant;

    public Task<IReadOnlyList<ToolSpec>> ListToolsAsync(CancellationToken ct) => real.ListToolsAsync(ct);

    public Task<ToolResult> CallAsync(ToolCall call, CancellationToken ct)
    {
        if (call.Name == RealTool) return real.CallAsync(call, ct);
        var hit = recordings.FirstOrDefault(r => r.Tool == call.Name &&
            (r.ArgsPattern is null || Regex.IsMatch(call.ArgumentsJson, r.ArgsPattern, Options)));
        return Task.FromResult(hit is null
            ? new ToolResult(call.Id, "No recording for this call.", true)
            : new ToolResult(call.Id, hit.Result, false));
    }
}
