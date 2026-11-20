using Glassbox.Adapters;
using Glassbox.Domain;

namespace Glassbox.Tests;

public class RetryOnceCompleterTests
{
    private sealed class ScriptedComplete(params Func<Func<string, ValueTask>, Task<ModelResponse>>[] attempts) : IComplete
    {
        public int Calls { get; private set; }

        public Task<ModelResponse> CompleteAsync(IReadOnlyList<AgentMessage> messages,
            IReadOnlyList<ToolSpec> tools, Func<string, ValueTask> onTextDelta, CancellationToken ct) =>
            attempts[Calls++](onTextDelta);
    }

    private static ModelResponse Ok(string text) => new(text, [], new ModelUsage(1, 1), "m");

    private static Task<ModelResponse> Fail(Exception e) => Task.FromException<ModelResponse>(e);

    private static Task<ModelResponse> Run(ScriptedComplete inner, List<string>? deltas = null,
        CancellationToken ct = default) =>
        new RetryOnceCompleter(inner).CompleteAsync([new(Role.User, "hi")], [],
            d => { deltas?.Add(d); return ValueTask.CompletedTask; }, ct);

    [Fact]
    public async Task Retries_once_after_ArgumentOutOfRange_and_returns_second_response()
    {
        var inner = new ScriptedComplete(_ => Fail(new ArgumentOutOfRangeException()), _ => Task.FromResult(Ok("second")));
        var r = await Run(inner);
        Assert.Equal("second", r.Text);
        Assert.Equal(2, inner.Calls);
    }

    [Fact]
    public async Task Second_failure_propagates_after_exactly_two_calls()
    {
        var inner = new ScriptedComplete(_ => Fail(new ArgumentOutOfRangeException()), _ => Fail(new ArgumentOutOfRangeException()));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => Run(inner));
        Assert.Equal(2, inner.Calls);
    }

    [Fact]
    public async Task No_retry_when_a_text_delta_was_already_forwarded()
    {
        var inner = new ScriptedComplete(async d =>
        {
            await d("partial");
            throw new ArgumentOutOfRangeException();
        });
        var deltas = new List<string>();
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => Run(inner, deltas));
        Assert.Equal(1, inner.Calls);
        Assert.Equal(["partial"], deltas);
    }

    [Fact]
    public async Task No_retry_for_other_exceptions()
    {
        var inner = new ScriptedComplete(_ => Fail(new InvalidOperationException("boom")));
        await Assert.ThrowsAsync<InvalidOperationException>(() => Run(inner));
        Assert.Equal(1, inner.Calls);
    }

    [Fact]
    public async Task No_retry_when_token_is_cancelled()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        var inner = new ScriptedComplete(_ => Fail(new ArgumentOutOfRangeException()));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => Run(inner, ct: cts.Token));
        Assert.Equal(1, inner.Calls);
    }
}
