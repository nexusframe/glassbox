using System.Net;
using Glassbox.Tests.Fakes;
using Glassbox.Tools.Mcp;

namespace Glassbox.Tests;

public class WeatherToolTests
{
    private const string Place = """{"results":[{"name":"Kraków","country":"Poland","latitude":50.06,"longitude":19.94}]}""";

    private static string Forecast(int code = 3) =>
        $$$"""{"current":{"time":"2026-11-22T23:15","temperature_2m":1.3,"weather_code":{{{code}}},"wind_speed_10m":6.8}}""";

    private static StubHttpHandler Stub(string place, string forecast) =>
        new(r => StubHttpHandler.Json(r.RequestUri!.Host.StartsWith("geocoding") ? place : forecast));

    [Fact]
    public async Task Formats_current_weather()
    {
        var stub = Stub(Place, Forecast());
        var result = await WeatherTool.CurrentAsync("Kraków", stub.Client(), CancellationToken.None);
        Assert.Equal("Kraków, Poland: 1.3°C, overcast, wind 6.8 km/h (local time 2026-11-22 23:15)", result);
    }

    [Fact]
    public async Task Encodes_city_with_polish_letters()
    {
        var stub = Stub(Place, Forecast());
        await WeatherTool.CurrentAsync("Zielona Góra", stub.Client(), CancellationToken.None);
        var query = stub.Requests[0].Query;
        Assert.Contains("name=Zielona%20G%C3%B3ra", query);
        Assert.Contains("count=1", query);
    }

    [Fact]
    public async Task Unknown_city_throws_argument_exception()
    {
        var stub = Stub("""{"generationtime_ms":0.1}""", Forecast());
        var ex = await Assert.ThrowsAsync<ArgumentException>(
            () => WeatherTool.CurrentAsync("Xyzzy", stub.Client(), CancellationToken.None));
        Assert.Contains("No place named", ex.Message);
    }

    [Fact]
    public async Task Unknown_weather_code_is_shown_as_number()
    {
        var stub = Stub(Place, Forecast(42));
        var result = await WeatherTool.CurrentAsync("Kraków", stub.Client(), CancellationToken.None);
        Assert.Contains("weather code 42", result);
    }

    [Fact]
    public async Task Timeout_maps_to_not_available()
    {
        var stub = new StubHttpHandler(_ => throw new TaskCanceledException());
        var ex = await Assert.ThrowsAsync<ServiceUnavailableException>(
            () => WeatherTool.CurrentAsync("Kraków", stub.Client(), CancellationToken.None));
        Assert.Equal("Open-Meteo is not available now. Try again later.", ex.Message);
    }

    [Fact]
    public async Task Server_error_maps_to_not_available()
    {
        var stub = new StubHttpHandler(_ => StubHttpHandler.Json("{}", HttpStatusCode.ServiceUnavailable));
        await Assert.ThrowsAsync<ServiceUnavailableException>(
            () => WeatherTool.CurrentAsync("Kraków", stub.Client(), CancellationToken.None));
    }

    [Fact]
    public async Task Empty_city_throws_argument_exception()
    {
        var stub = Stub(Place, Forecast());
        await Assert.ThrowsAsync<ArgumentException>(
            () => WeatherTool.CurrentAsync("  ", stub.Client(), CancellationToken.None));
        Assert.Empty(stub.Requests);
    }

    [Fact]
    public async Task Slow_second_request_fails_within_the_tool_budget()
    {
        var stub = new StubHttpHandler(async (r, ct) =>
        {
            if (!r.RequestUri!.Host.StartsWith("geocoding")) await Task.Delay(Timeout.Infinite, ct);
            return StubHttpHandler.Json(Place);
        });
        var watch = System.Diagnostics.Stopwatch.StartNew();
        await Assert.ThrowsAsync<ServiceUnavailableException>(() =>
            WeatherTool.CurrentAsync("Kraków", stub.Client(), CancellationToken.None, TimeSpan.FromMilliseconds(200)));
        Assert.True(watch.Elapsed < TimeSpan.FromSeconds(3), $"took {watch.Elapsed}");
    }

    [Fact]
    public async Task Missing_temperature_maps_to_not_available()
    {
        var stub = Stub(Place, """{"current":{"time":"2026-11-22T23:15","temperature_2m":null,"weather_code":3,"wind_speed_10m":6.8}}""");
        await Assert.ThrowsAsync<ServiceUnavailableException>(
            () => WeatherTool.CurrentAsync("Kraków", stub.Client(), CancellationToken.None));
    }

    [Fact]
    public async Task Not_found_from_open_meteo_maps_to_not_available()
    {
        var stub = new StubHttpHandler(_ => StubHttpHandler.Json("{}", HttpStatusCode.NotFound));
        await Assert.ThrowsAsync<ServiceUnavailableException>(
            () => WeatherTool.CurrentAsync("Kraków", stub.Client(), CancellationToken.None));
    }
}
