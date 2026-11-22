using System.ComponentModel;
using ModelContextProtocol;
using ModelContextProtocol.Server;

namespace Glassbox.Tools.Mcp;

[McpServerToolType]
public sealed class GlassboxTools(TimeProvider time, IHttpClientFactory http)
{
    public const string HttpClientName = "public-apis";

    [McpServerTool(Name = "calculate"), Description("Evaluates plain arithmetic with + - * / and parentheses, e.g. 240*0.17. Use a dot as decimal separator. No variables or functions.")]
    public string Calculate([Description("Arithmetic expression, e.g. (2+3)*4")] string expression)
    {
        try { return Calculator.Evaluate(expression).ToString(System.Globalization.CultureInfo.InvariantCulture); }
        catch (FormatException e) { throw new McpException(e.Message); }
    }

    [McpServerTool(Name = "get_datetime"), Description("Returns the current date and time in a time zone. Use an IANA zone id such as Europe/Warsaw or Asia/Tokyo.")]
    public string GetDateTime([Description("IANA time zone id, e.g. Europe/Warsaw")] string timeZone = "UTC")
    {
        try { return DateTimeTool.Now(timeZone, time); }
        catch (ArgumentException e) { throw new McpException(e.Message); }
    }

    [McpServerTool(Name = "get_weather"), Description("Returns the current weather for a city: temperature, sky and wind. Give the city name in its basic form, e.g. Kraków or Paris.")]
    public async Task<string> GetWeatherAsync([Description("City name, e.g. Kraków")] string city, CancellationToken ct)
    {
        try { return await WeatherTool.CurrentAsync(city, http.CreateClient(HttpClientName), ct); }
        catch (Exception e) when (e is ArgumentException or ServiceUnavailableException) { throw new McpException(e.Message); }
    }

    [McpServerTool(Name = "convert_currency"), Description("Converts an amount between currencies at the latest NBP (National Bank of Poland) mid rate. Use ISO codes such as PLN, USD, EUR. For further arithmetic on the result, use calculate.")]
    public async Task<string> ConvertCurrencyAsync(
        [Description("Amount, e.g. 100")] decimal amount,
        [Description("ISO code to convert from, e.g. USD")] string from,
        [Description("ISO code to convert to, e.g. PLN")] string to,
        CancellationToken ct)
    {
        try { return await CurrencyTool.ConvertAsync(amount, from, to, http.CreateClient(HttpClientName), ct); }
        catch (Exception e) when (e is ArgumentException or ServiceUnavailableException) { throw new McpException(e.Message); }
    }

    [McpServerTool(Name = "wiki_summary"), Description("Returns the first paragraph of a Wikipedia article. Use for facts about people, places and things. Give the exact article title.")]
    public async Task<string> WikiSummaryAsync(
        [Description("Article title, e.g. Kraków")] string title,
        [Description("Wikipedia language: pl or en")] string language = "en",
        CancellationToken ct = default)
    {
        try { return await WikiTool.SummaryAsync(title, language, http.CreateClient(HttpClientName), ct); }
        catch (Exception e) when (e is ArgumentException or ServiceUnavailableException) { throw new McpException(e.Message); }
    }
}
