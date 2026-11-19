using Glassbox.Domain;
using Microsoft.Extensions.Time.Testing;

namespace Glassbox.Tests;

public class DailyBudgetTests
{
    private sealed class MemoryStore : IBudgetStore
    {
        private BudgetDay? _day;
        public Task<BudgetDay?> LoadAsync(CancellationToken ct) => Task.FromResult(_day);
        public Task SaveAsync(BudgetDay day, CancellationToken ct) { _day = day; return Task.CompletedTask; }
    }

    private static FakeTimeProvider At(int y, int m, int d, int h = 12, int min = 0) =>
        new(new DateTimeOffset(y, m, d, h, min, 0, TimeSpan.Zero));

    [Fact]
    public async Task Can_spend_until_limit()
    {
        var b = new DailyBudget(new MemoryStore(), 0.50m, At(2026, 11, 19));
        await b.AddAsync(0.30m, default);
        Assert.True(await b.CanSpendAsync(default));
        await b.AddAsync(0.20m, default);
        Assert.False(await b.CanSpendAsync(default));
    }

    [Fact]
    public async Task Resets_on_new_utc_day()
    {
        var time = At(2026, 11, 19, 23, 59);
        var b = new DailyBudget(new MemoryStore(), 0.50m, time);
        await b.AddAsync(0.50m, default);
        Assert.False(await b.CanSpendAsync(default));
        time.Advance(TimeSpan.FromMinutes(2));
        Assert.True(await b.CanSpendAsync(default));
        Assert.Equal(0m, await b.UsedFractionAsync(default));
    }

    [Fact]
    public async Task Survives_restart()
    {
        var store = new MemoryStore();
        var time = At(2026, 11, 19);
        await new DailyBudget(store, 0.50m, time).AddAsync(0.30m, default);
        Assert.Equal(0.6m, await new DailyBudget(store, 0.50m, time).UsedFractionAsync(default));
    }

    [Fact]
    public async Task Used_fraction_is_capped()
    {
        var b = new DailyBudget(new MemoryStore(), 0.50m, At(2026, 11, 19));
        await b.AddAsync(0.70m, default);
        Assert.Equal(1m, await b.UsedFractionAsync(default));
    }
}
