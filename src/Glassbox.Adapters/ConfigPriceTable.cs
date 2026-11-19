using Glassbox.Domain;

namespace Glassbox.Adapters;

public sealed record ModelPrice(decimal InputPerMTok, decimal OutputPerMTok);

public sealed class ConfigPriceTable(IReadOnlyDictionary<string, ModelPrice> prices) : IPriceTable
{
    public decimal CostUsd(string modelId, ModelUsage usage)
    {
        if (!prices.TryGetValue(modelId, out var p))
            throw new InvalidOperationException($"No price configured for model '{modelId}'.");
        return (usage.InputTokens * p.InputPerMTok + usage.OutputTokens * p.OutputPerMTok) / 1_000_000m;
    }
}
