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
            if (response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.UnprocessableEntity)
                throw new ArgumentException($"{service} rejected the input. Change it and try again.");
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

    // True for errors that mean the service answered in a shape we do not expect
    // (missing or null property, empty array, 404 on a fixed URL). Tools map them to "not available".
    public static bool IsUnexpectedAnswer(Exception e) =>
        e is KeyNotFoundException or InvalidOperationException or IndexOutOfRangeException
            or FormatException or PublicApiNotFoundException;
}
