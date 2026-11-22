using System.Globalization;

namespace Glassbox.Tools.Mcp;

// Currency conversion at the latest NBP table A mid rates. PLN is the base (rate 1).
public static class CurrencyTool
{
    private const string Service = "NBP";
    private const decimal MaxAmount = 1_000_000_000_000m;

    public static async Task<string> ConvertAsync(decimal amount, string from, string to, HttpClient http, CancellationToken ct)
    {
        if (amount <= 0 || amount > MaxAmount)
            throw new ArgumentException("Give an amount greater than 0 and at most 1000000000000.", nameof(amount));
        from = (from ?? "").Trim().ToUpperInvariant();
        to = (to ?? "").Trim().ToUpperInvariant();
        var shown = amount.ToString(CultureInfo.InvariantCulture);
        if (from == to) return $"{shown} {from} = {Format(amount)} {to}";

        var table = (await PublicApi.GetJsonAsync(http, "https://api.nbp.pl/api/exchangerates/tables/A/?format=json", Service, ct))[0];
        var rates = new Dictionary<string, decimal> { ["PLN"] = 1m };
        foreach (var r in table.GetProperty("rates").EnumerateArray())
            rates[r.GetProperty("code").GetString()!] = r.GetProperty("mid").GetDecimal();

        decimal Rate(string code) => rates.TryGetValue(code, out var mid) ? mid
            : throw new ArgumentException($"Currency '{code}' is not in NBP table A. Use an ISO code such as PLN, USD or EUR.");

        var result = amount * Rate(from) / Rate(to);
        return $"{shown} {from} = {Format(result)} {to} (NBP mid rate, table {table.GetProperty("no").GetString()}, {table.GetProperty("effectiveDate").GetString()})";
    }

    private static string Format(decimal value) =>
        Math.Round(value, 2, MidpointRounding.AwayFromZero).ToString("0.00", CultureInfo.InvariantCulture);
}
