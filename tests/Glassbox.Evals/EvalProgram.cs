extern alias api;

using api::Glassbox.Api;
using Glassbox.Adapters;
using Glassbox.Domain;
using Glassbox.Tools.Mcp;
using Microsoft.Extensions.Configuration;

namespace Glassbox.Evals;

/// <summary>
/// Manual eval run on the real model (ADR 0006). Usage:
/// dotnet run --project tests/Glassbox.Evals [--runs 5] [--only A1,C11]
/// </summary>
public static class EvalProgram
{
    private const string KeyName = "OPENROUTER_API_KEY";

    public static async Task<int> Main(string[] args)
    {
        var key = Environment.GetEnvironmentVariable(KeyName);
        if (string.IsNullOrWhiteSpace(key))
        {
            await Console.Error.WriteLineAsync($"{KeyName} is not set.");
            return 2;
        }
        var runs = int.Parse(Arg(args, "--runs") ?? "5", System.Globalization.CultureInfo.InvariantCulture);
        var only = Arg(args, "--only")?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var cases = Cases.All.Where(c => only is null || only.Contains(c.Id, StringComparer.OrdinalIgnoreCase)).ToList();

        // The production settings: model id, output tokens, limits and prices.
        var o = new GlassboxOptions();
        new ConfigurationBuilder().SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false).Build()
            .GetSection("Glassbox").Bind(o);

        await using var server = ToolsServer.Build([], "http://127.0.0.1:0");
        await server.StartAsync();
        var port = new Uri(server.Urls.First()).Port;
        await using var tools = new McpToolCaller(new Uri($"http://127.0.0.1:{port}/mcp"));

        var runner = new EvalRunner(api::LazyOpenRouterCompleter.Create(key, o), tools, new ConfigPriceTable(o.Prices),
            new RunLimits(o.MaxTurns, o.MaxToolCallsPerTurn, TimeSpan.FromSeconds(o.MaxRunSeconds)), SystemPrompt.Text);
        var results = new List<CaseResult>();
        foreach (var c in cases)
        {
            var r = await runner.RunCaseAsync(c, runs, CancellationToken.None);
            results.Add(r);
            Console.WriteLine($"{c.Id} {r.Grades.Count(g => g.Pass)}/{runs}");
        }

        var date = DateOnly.FromDateTime(DateTime.UtcNow);
        var md = Report.Markdown(o.ModelId, date, runs, results);
        Console.WriteLine();
        Console.WriteLine(md);
        var dir = Path.Combine("tests", "Glassbox.Evals", "results");
        Directory.CreateDirectory(dir);
        var file = Path.Combine(dir, $"{date:yyyy-MM-dd}.md");
        await File.WriteAllTextAsync(file, md);
        Console.WriteLine($"Report: {file}");
        return 0;
    }

    private static string? Arg(string[] args, string name)
    {
        var i = Array.IndexOf(args, name);
        return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
    }
}
