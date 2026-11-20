using Glassbox.Domain;

namespace Glassbox.Adapters;

/// <summary>
/// Calls the model again once when the provider ends a stream with an error finish reason, which the SDK reports as ArgumentOutOfRangeException.
/// It retries only before any text reaches the caller, because a retry after that would repeat text on the page.
/// </summary>
public sealed class RetryOnceCompleter(IComplete inner) : IComplete
{
    public async Task<ModelResponse> CompleteAsync(IReadOnlyList<AgentMessage> messages,
        IReadOnlyList<ToolSpec> tools, Func<string, ValueTask> onTextDelta, CancellationToken ct)
    {
        var forwarded = false;
        async ValueTask Forward(string delta)
        {
            forwarded = true;
            await onTextDelta(delta);
        }

        try
        {
            return await inner.CompleteAsync(messages, tools, Forward, ct);
        }
        catch (ArgumentOutOfRangeException) when (!forwarded && !ct.IsCancellationRequested)
        {
            return await inner.CompleteAsync(messages, tools, onTextDelta, ct);
        }
    }
}
