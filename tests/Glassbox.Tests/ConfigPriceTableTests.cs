using Glassbox.Adapters;
using Glassbox.Domain;

namespace Glassbox.Tests;

public class ConfigPriceTableTests
{
    private static ConfigPriceTable Table() =>
        new(new Dictionary<string, ModelPrice> { ["m"] = new(1.00m, 5.00m) });

    [Fact]
    public void Million_input_tokens_cost_input_price() =>
        Assert.Equal(1.00m, Table().CostUsd("m", new ModelUsage(1_000_000, 0)));

    [Fact]
    public void Mixed_usage_sums_input_and_output() =>
        Assert.Equal(0.002m, Table().CostUsd("m", new ModelUsage(1000, 200)));

    [Fact]
    public void Unknown_model_throws() =>
        Assert.Throws<InvalidOperationException>(() => Table().CostUsd("other", new ModelUsage(1, 1)));
}
