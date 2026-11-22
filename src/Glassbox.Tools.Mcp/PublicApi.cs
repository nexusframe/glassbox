using System.Net;
using System.Text.Json;

namespace Glassbox.Tools.Mcp;

// The external service failed (network error, timeout or 5xx). The model can try again later.
public sealed class ServiceUnavailableException(string service)
    : Exception($"{service} is not available now. Try again later.");

// The external service answered 404.
public sealed class PublicApiNotFoundException(string service) : Exception($"{service} returned 404.");

// Shared GET + JSON for the tools that call free public APIs.
internal static class PublicApi
{
    public static async Task<JsonElement> GetJsonAsync(HttpClient http, string url, string service, CancellationToken ct)
    {
        try
        {
            using var response = await http.GetAsync(url, ct);
            if (response.StatusCode == HttpStatusCode.NotFound) throw new PublicApiNotFoundException(service);
            if ((int)response.StatusCode >= 500) throw new ServiceUnavailableException(service);
            response.EnsureSuccessStatusCode();
            await using var body = await response.Content.ReadAsStreamAsync(ct);
            using var doc = await JsonDocument.ParseAsync(body, cancellationToken: ct);
            return doc.RootElement.Clone();
        }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested)
        {
            throw new ServiceUnavailableException(service); // HttpClient timeout
        }
        catch (Exception e) when (e is HttpRequestException or JsonException)
        {
            throw new ServiceUnavailableException(service);
        }
    }
}
