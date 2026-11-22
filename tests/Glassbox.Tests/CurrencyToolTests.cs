using Glassbox.Tests.Fakes;
using Glassbox.Tools.Mcp;

namespace Glassbox.Tests;

public class CurrencyToolTests
{
    private const string Table = """[{"table":"A","no":"226/A/NBP/2026","effectiveDate":"2026-11-20","rates":[{"currency":"dolar amerykański","code":"USD","mid":4.0613},{"currency":"euro","code":"EUR","mid":4.2500}]}]""";

    private static StubHttpHandler Stub() => new(_ => StubHttpHandler.Json(Table));

    private static Task<string> Convert(decimal amount, string from, string to, StubHttpHandler? stub = null) =>
        CurrencyTool.ConvertAsync(amount, from, to, (stub ?? Stub()).Client(), CancellationToken.None);

    [Fact]
    public async Task Converts_to_pln() =>
        Assert.Equal("100 USD = 406.13 PLN (NBP mid rate, table 226/A/NBP/2026, 2026-11-20)", await Convert(100, "USD", "PLN"));

    [Fact]
    public async Task Converts_from_pln() =>
        Assert.StartsWith("406.13 PLN = 100.00 USD", await Convert(406.13m, "PLN", "USD"));

    [Fact]
    public async Task Cross_rate_through_pln() =>
        Assert.StartsWith("100 EUR = 104.65 USD", await Convert(100, "EUR", "USD"));

    [Fact]
    public async Task Codes_are_case_insensitive() =>
        Assert.StartsWith("100 USD = 406.13 PLN", await Convert(100, "usd", "pln"));

    [Fact]
    public async Task Unknown_code_throws()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() => Convert(100, "XYZ", "PLN"));
        Assert.Contains("XYZ", ex.Message);
        Assert.Contains("NBP table A", ex.Message);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public async Task Non_positive_amount_throws(int amount) =>
        await Assert.ThrowsAsync<ArgumentException>(() => Convert(amount, "USD", "PLN"));

    [Fact]
    public async Task Same_currency_needs_no_request()
    {
        var stub = Stub();
        Assert.Equal("50 PLN = 50.00 PLN", await Convert(50, "PLN", "PLN", stub));
        Assert.Empty(stub.Requests);
    }

    [Theory]
    [InlineData("[]")]
    [InlineData("{}")]
    [InlineData("""[{"no":"1","effectiveDate":"2026-11-20","rates":[{"code":"USD","mid":null}]}]""")]
    public async Task Unexpected_json_maps_to_not_available(string body) =>
        await Assert.ThrowsAsync<ServiceUnavailableException>(
            () => Convert(100, "USD", "PLN", new StubHttpHandler(_ => StubHttpHandler.Json(body))));
}
