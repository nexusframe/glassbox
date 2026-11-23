using System.Globalization;
using System.Text;

namespace Glassbox.Evals;

public static class Report
{
    public static string Markdown(string modelId, DateOnly date, int runs, IReadOnlyList<CaseResult> results)
    {
        var grades = results.SelectMany(r => r.Grades).ToList();
        var cost = results.SelectMany(r => r.Runs).Sum(r => r.CostUsd);
        var all = grades.Count;
        var sb = new StringBuilder();
        sb.AppendLine("## Eval results").AppendLine();
        sb.AppendLine(Inv($"Model: {modelId}. Date: {date:yyyy-MM-dd}. Runs per case: {runs}. Cost: {cost:0.0000} USD."));
        sb.AppendLine();
        sb.AppendLine("| Metric | Value |").AppendLine("|---|---|");
        sb.AppendLine($"| Pass rate | {Ratio(grades.Count(g => g.Pass), all)} |");
        sb.AppendLine($"| Tool choice | {Ratio(grades.Count(g => g.ToolsOk), all)} |");
        sb.AppendLine($"| Facts present | {Ratio(grades.Count(g => g.FactsOk), all)} |");
        sb.AppendLine($"| Stable cases | {results.Count(r => r.Grades.All(g => g.Pass))}/{results.Count} |");
        sb.AppendLine();
        sb.AppendLine("| Case | Input | Pass | Failure reasons |").AppendLine("|---|---|---|---|");
        foreach (var r in results)
        {
            var reasons = r.Grades.SelectMany(g => g.Reasons).GroupBy(x => x)
                .Select(g => $"{g.Key} ({g.Count()})");
            sb.AppendLine($"| {r.Case.Id} | {Cell(r.Case.Input)} | {r.Grades.Count(g => g.Pass)}/{r.Grades.Count} | {Cell(string.Join("; ", reasons))} |");
        }
        // The answers of failed runs. Reason: a reader must see if the model or the pattern is wrong.
        var failed = results.SelectMany(r => r.Grades.Select((g, i) => (r, g, i)).Where(x => !x.g.Pass)).ToList();
        if (failed.Count > 0)
        {
            sb.AppendLine().AppendLine("### Failed answers").AppendLine();
            foreach (var (r, _, i) in failed)
                sb.AppendLine($"- {r.Case.Id} run {i + 1}: {OneLine(r.Runs[i].Answer ?? "(no answer)")}");
        }
        return sb.ToString();
    }

    private static string Ratio(int n, int all) =>
        $"{n}/{all} ({(all == 0 ? 0 : Math.Round(100m * n / all, MidpointRounding.AwayFromZero)):0}%)";

    private static string Cell(string text) => OneLine(text).Replace("|", @"\|");

    private static string OneLine(string text) =>
        string.Join(' ', text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

    private static string Inv(FormattableString s) => s.ToString(CultureInfo.InvariantCulture);
}
