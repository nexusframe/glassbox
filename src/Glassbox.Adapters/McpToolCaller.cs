using System.Text;
using System.Text.Json;
using Glassbox.Domain;
using ModelContextProtocol;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

namespace Glassbox.Adapters;

/// <summary>
/// MCP client over streamable HTTP. Connects lazily, caches the tool list, and on a transport
/// failure reconnects once and retries once (the server keeps sessions in memory only).
/// </summary>
public sealed class McpToolCaller(Uri endpoint, TimeSpan callTimeout) : ICallTool, IAsyncDisposable
{
    private const string GenericFailure = "Tool failed.";

    private readonly SemaphoreSlim _connectGate = new(1, 1);
    private McpClient? _client;
    private IReadOnlyList<ToolSpec>? _tools;

    public McpToolCaller(Uri endpoint) : this(endpoint, TimeSpan.FromSeconds(10)) { }

    public async Task<IReadOnlyList<ToolSpec>> ListToolsAsync(CancellationToken ct)
    {
        if (_tools is { } cached) return cached;
        // Server down: after one reconnect+retry the last exception propagates (the loop reports a run error).
        var specs = await WithRetryAsync(async (client, token) =>
        {
            var tools = await client.ListToolsAsync(cancellationToken: token);
            return (IReadOnlyList<ToolSpec>)tools.Select(t => new ToolSpec(
                t.Name, t.Description ?? "", t.ProtocolTool.InputSchema.GetRawText())).ToList();
        }, ct);
        _tools = specs;
        return specs;
    }

    public async Task<ToolResult> CallAsync(ToolCall call, CancellationToken ct)
    {
        Dictionary<string, object?>? args;
        try
        {
            args = string.IsNullOrWhiteSpace(call.ArgumentsJson)
                ? []
                : JsonSerializer.Deserialize<Dictionary<string, object?>>(call.ArgumentsJson);
        }
        catch (JsonException) { return new ToolResult(call.Id, "Invalid tool arguments: not valid JSON.", true); }
        if (args is null) return new ToolResult(call.Id, "Invalid tool arguments: expected a JSON object.", true);

        try
        {
            return await WithRetryAsync(async (client, token) =>
            {
                try
                {
                    var r = await client.CallToolAsync(call.Name, args, cancellationToken: token);
                    return new ToolResult(call.Id, TextOf(r), r.IsError == true);
                }
                catch (McpProtocolException e) when (e.ErrorCode is McpErrorCode.InvalidParams or McpErrorCode.MethodNotFound)
                {
                    // Unknown tool / bad arguments: the session is fine, so no reconnect.
                    return new ToolResult(call.Id, e.Message, true);
                }
            }, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception) { return new ToolResult(call.Id, GenericFailure, true); }
    }

    public async ValueTask DisposeAsync()
    {
        McpClient? c;
        await _connectGate.WaitAsync();
        try { c = _client; _client = null; }
        finally { _connectGate.Release(); }
        if (c is not null) await c.DisposeAsync();
    }

    private async Task<T> WithRetryAsync<T>(Func<McpClient, CancellationToken, Task<T>> op, CancellationToken ct)
    {
        for (var attempt = 0; ; attempt++)
        {
            ct.ThrowIfCancellationRequested();
            McpClient? client = null;
            try
            {
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                cts.CancelAfter(callTimeout);
                client = await GetClientAsync(cts.Token);
                return await op(client, cts.Token);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
            catch (Exception) when (attempt == 0)
            {
                await InvalidateAsync(client);
            }
            catch (Exception)
            {
                await InvalidateAsync(client);
                throw;
            }
        }
    }

    private async Task<McpClient> GetClientAsync(CancellationToken ct)
    {
        if (_client is { } existing) return existing;
        await _connectGate.WaitAsync(ct);
        try
        {
            if (_client is { } c) return c;
            var transport = new HttpClientTransport(new HttpClientTransportOptions { Endpoint = endpoint });
            _client = await McpClient.CreateAsync(transport, cancellationToken: ct);
            return _client;
        }
        finally { _connectGate.Release(); }
    }

    private async Task InvalidateAsync(McpClient? failed)
    {
        _tools = null;
        McpClient? toDispose = null;
        await _connectGate.WaitAsync();
        try
        {
            if (failed is not null && ReferenceEquals(_client, failed)) { toDispose = _client; _client = null; }
        }
        finally { _connectGate.Release(); }
        if (toDispose is null) return;
        try { await toDispose.DisposeAsync(); } catch { /* already broken */ }
    }

    private static string TextOf(CallToolResult r)
    {
        var sb = new StringBuilder();
        foreach (var block in r.Content)
            if (block is TextContentBlock t) sb.Append(t.Text);
        return sb.ToString();
    }
}
