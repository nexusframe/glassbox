using Glassbox.Adapters;
using Glassbox.Domain;

namespace Glassbox.Tests;

public class FileBudgetStoreTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "glassbox-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, true);
    }

    [Fact]
    public async Task FileBudgetStore_round_trips()
    {
        var path = Path.Combine(_dir, "sub", "budget.json");
        var day = new BudgetDay(new DateOnly(2026, 11, 19), 0.12m);
        await new FileBudgetStore(path).SaveAsync(day, default);
        Assert.Equal(day, await new FileBudgetStore(path).LoadAsync(default));
        Assert.Null(await new FileBudgetStore(Path.Combine(_dir, "missing.json")).LoadAsync(default));
    }
}
