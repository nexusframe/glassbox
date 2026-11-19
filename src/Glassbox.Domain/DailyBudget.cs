namespace Glassbox.Domain;

public sealed class DailyBudget(IBudgetStore store, decimal dailyLimitUsd, TimeProvider time) : IBudget
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private BudgetDay? _current;
    private bool _loaded;

    public async Task<bool> CanSpendAsync(CancellationToken ct)
    {
        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try { return (await SpentTodayAsync(ct).ConfigureAwait(false)) < dailyLimitUsd; }
        finally { _gate.Release(); }
    }

    public async Task AddAsync(decimal usd, CancellationToken ct)
    {
        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var spent = await SpentTodayAsync(ct).ConfigureAwait(false);
            var day = new BudgetDay(Today(), spent + usd);
            await store.SaveAsync(day, ct).ConfigureAwait(false);
            _current = day;
        }
        finally { _gate.Release(); }
    }

    public async Task<decimal> UsedFractionAsync(CancellationToken ct)
    {
        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var spent = await SpentTodayAsync(ct).ConfigureAwait(false);
            if (dailyLimitUsd <= 0) return 1m;
            return Math.Min(1m, spent / dailyLimitUsd);
        }
        finally { _gate.Release(); }
    }

    private DateOnly Today() => DateOnly.FromDateTime(time.GetUtcNow().UtcDateTime);

    private async Task<decimal> SpentTodayAsync(CancellationToken ct)
    {
        if (!_loaded)
        {
            _current = await store.LoadAsync(ct).ConfigureAwait(false);
            _loaded = true;
        }
        return _current is { } d && d.DateUtc == Today() ? d.SpentUsd : 0m;
    }
}
