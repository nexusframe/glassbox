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
        return sb.ToString();
    }

    private static string Ratio(int n, int all) =>
        $"{n}/{all} ({(all == 0 ? 0 : Math.Round(100m * n / all, MidpointRounding.AwayFromZero)):0}%)";

    private static string Cell(string text) => text.Replace("|", @"\|").Replace("\n", " ");

    private static string Inv(FormattableString s) => s.ToString(CultureInfo.InvariantCulture);
}
