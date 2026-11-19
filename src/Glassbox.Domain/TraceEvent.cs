using System.Text.Json;
using System.Text.Json.Serialization;

namespace Glassbox.Domain;

public enum TraceType { Run, Guard, Model, Tool, AnswerDelta, Error }

public enum TracePhase { Start, End }

public enum TraceStatus { Ok, Error, Blocked }

public sealed record TraceEvent(TraceType Type, string Id, string RunId, int StartMs)
{
    public TracePhase? Phase { get; init; }
    public string? ParentId { get; init; }
    public int? Turn { get; init; }
    public int? EndMs { get; init; }
    public TraceStatus? Status { get; init; }
    public string? Name { get; init; }
    public int? InputTokens { get; init; }
    public int? OutputTokens { get; init; }
    public decimal? CostUsd { get; init; }
    public decimal? BudgetUsed { get; init; }
    public string? Args { get; init; }
    public string? Text { get; init; }
}

public static class TraceJson
{
    public static JsonSerializerOptions Options { get; } = Create();

    private static JsonSerializerOptions Create()
    {
        var o = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        };
        o.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower));
        o.MakeReadOnly(populateMissingResolver: true);
        return o;
    }
}
