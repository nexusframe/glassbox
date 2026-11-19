namespace Glassbox.Domain;

public interface IComplete
{
    Task<ModelResponse> CompleteAsync(IReadOnlyList<AgentMessage> messages,
        IReadOnlyList<ToolSpec> tools, Func<string, ValueTask> onTextDelta,
        CancellationToken ct);
}

public interface ICallTool
{
    Task<IReadOnlyList<ToolSpec>> ListToolsAsync(CancellationToken ct);
    Task<ToolResult> CallAsync(ToolCall call, CancellationToken ct); // never throws for tool errors
}

public interface ITraceSink
{
    ValueTask EmitAsync(TraceEvent e, CancellationToken ct);
}

public interface IBudget
{
    Task<bool> CanSpendAsync(CancellationToken ct);
    Task AddAsync(decimal usd, CancellationToken ct);
    Task<decimal> UsedFractionAsync(CancellationToken ct); // 0..1, capped at 1
}

public sealed record BudgetDay(DateOnly DateUtc, decimal SpentUsd);

public interface IBudgetStore
{
    Task<BudgetDay?> LoadAsync(CancellationToken ct);
    Task SaveAsync(BudgetDay day, CancellationToken ct);
}

public interface IPriceTable
{
    decimal CostUsd(string modelId, ModelUsage usage);
}
