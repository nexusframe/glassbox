using System.Text;
using System.Text.Json;
using Glassbox.Domain;
using Microsoft.AspNetCore.Http;

namespace Glassbox.Api;

/// <summary>One JSON line per event, flushed immediately. Write errors (client gone) are swallowed.</summary>
public sealed class NdjsonTraceSink(HttpResponse response, string sessionId) : ITraceSink
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private bool _started, _dead;

    public async ValueTask EmitAsync(TraceEvent e, CancellationToken ct)
    {
        var bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(e, TraceJson.Options) + "\n");
        await _gate.WaitAsync(CancellationToken.None).ConfigureAwait(false);
        try
        {
            if (_dead) return;
            if (!_started)
            {
                response.StatusCode = StatusCodes.Status200OK;
                response.ContentType = "application/x-ndjson";
                response.Headers["X-Session-Id"] = sessionId;
                response.Headers.CacheControl = "no-cache";
                response.Headers["X-Accel-Buffering"] = "no";
                _started = true;
            }
            await response.Body.WriteAsync(bytes, CancellationToken.None).ConfigureAwait(false);
            await response.Body.FlushAsync(CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception) { _dead = true; }
        finally { _gate.Release(); }
    }
}
