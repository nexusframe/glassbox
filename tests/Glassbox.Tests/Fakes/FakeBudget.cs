using Glassbox.Domain;

namespace Glassbox.Tests.Fakes;

public sealed class FakeBudget : IBudget
{
    public bool CanSpend { get; set; } = true;
    public List<decimal> Added { get; } = [];

    public Task<bool> CanSpendAsync(CancellationToken ct) => Task.FromResult(CanSpend);
    public Task AddAsync(decimal usd, CancellationToken ct) { Added.Add(usd); return Task.CompletedTask; }
    public Task<decimal> UsedFractionAsync(CancellationToken ct) => Task.FromResult(0.25m);
}

/// <summary>Cost = tokens * fixed rate, so tests can compute expected values.</summary>
public sealed class FixedPrices(decimal perToken = 0.001m) : IPriceTable
{
    public decimal CostUsd(string modelId, ModelUsage usage) =>
        (usage.InputTokens + usage.OutputTokens) * perToken;
}
