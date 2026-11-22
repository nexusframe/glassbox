using System.Globalization;
using System.Text.Json;

namespace Glassbox.Tools.Mcp;

// Current weather from Open-Meteo: geocode the city, then read the current conditions.
public static class WeatherTool
{
    private const string Service = "Open-Meteo";

    // One budget for both requests; stays below the MCP client call timeout (10 s).
    private static readonly TimeSpan DefaultBudget = TimeSpan.FromSeconds(8);

    private static readonly Dictionary<int, string> Codes = new()
    {
        [0] = "clear sky", [1] = "mainly clear", [2] = "partly cloudy", [3] = "overcast",
        [45] = "fog", [48] = "fog",
        [51] = "drizzle", [53] = "drizzle", [55] = "drizzle", [56] = "freezing drizzle", [57] = "freezing drizzle",
        [61] = "rain", [63] = "rain", [65] = "rain", [66] = "freezing rain", [67] = "freezing rain",
        [71] = "snow", [73] = "snow", [75] = "snow", [77] = "snow grains",
        [80] = "rain showers", [81] = "rain showers", [82] = "rain showers", [85] = "snow showers", [86] = "snow showers",
        [95] = "thunderstorm", [96] = "thunderstorm with hail", [99] = "thunderstorm with hail",
    };

    public static async Task<string> CurrentAsync(string city, HttpClient http, CancellationToken ct, TimeSpan? budget = null)
    {
        var name = city?.Trim() ?? "";
        if (name.Length is 0 or > 100)
            throw new ArgumentException("Give a city name of 1 to 100 characters, e.g. Kraków.", nameof(city));

        using var limit = CancellationTokenSource.CreateLinkedTokenSource(ct);
        limit.CancelAfter(budget ?? DefaultBudget);
        try { return await FetchAsync(name, http, limit.Token); }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested) { throw new ServiceUnavailableException(Service); }
        catch (Exception e) when (PublicApi.IsUnexpectedAnswer(e)) { throw new ServiceUnavailableException(Service); }
    }

    private static async Task<string> FetchAsync(string name, HttpClient http, CancellationToken ct)
    {
        var places = await PublicApi.GetJsonAsync(http,
            $"https://geocoding-api.open-meteo.com/v1/search?name={Uri.EscapeDataString(name)}&count=1&language=pl&format=json",
            Service, ct);
        if (!places.TryGetProperty("results", out var results) || results.GetArrayLength() == 0)
            throw new ArgumentException($"No place named '{name}'. Check the spelling or use the basic form of the name.", "city");

        var place = results[0];
        var lat = place.GetProperty("latitude").GetDouble().ToString(CultureInfo.InvariantCulture);
        var lon = place.GetProperty("longitude").GetDouble().ToString(CultureInfo.InvariantCulture);
        var forecast = await PublicApi.GetJsonAsync(http,
            $"https://api.open-meteo.com/v1/forecast?latitude={lat}&longitude={lon}&current=temperature_2m,weather_code,wind_speed_10m&timezone=auto",
            Service, ct);

        var now = forecast.GetProperty("current");
        var code = now.GetProperty("weather_code").GetInt32();
        var sky = Codes.TryGetValue(code, out var text) ? text : $"weather code {code}";
        var country = place.TryGetProperty("country", out var c) ? $", {c.GetString()}" : "";
        var time = now.GetProperty("time").GetString()?.Replace('T', ' ');
        return string.Create(CultureInfo.InvariantCulture,
            $"{place.GetProperty("name").GetString()}{country}: {now.GetProperty("temperature_2m").GetDouble()}°C, {sky}, wind {now.GetProperty("wind_speed_10m").GetDouble()} km/h (local time {time})");
    }
}
