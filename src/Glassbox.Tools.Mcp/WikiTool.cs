namespace Glassbox.Tools.Mcp;

// First paragraph of a Wikipedia article, from the REST page summary endpoint.
// The text is written by third parties; the loop treats it as data (ADR 0004).
public static class WikiTool
{
    private const string Service = "Wikipedia";
    private const int MaxSummary = 800;

    // The host name is built from the language, so only a fixed list is allowed.
    private static readonly HashSet<string> Languages = ["pl", "en"];

    public static async Task<string> SummaryAsync(string title, string language, HttpClient http, CancellationToken ct)
    {
        language = (language ?? "").Trim().ToLowerInvariant();
        if (!Languages.Contains(language))
            throw new ArgumentException("Wikipedia language must be pl or en.", nameof(language));
        var name = title?.Trim() ?? "";
        if (name.Length is 0 or > 200)
            throw new ArgumentException("Give an article title of 1 to 200 characters.", nameof(title));

        var url = $"https://{language}.wikipedia.org/api/rest_v1/page/summary/{Uri.EscapeDataString(name.Replace(' ', '_'))}";
        System.Text.Json.JsonElement page;
        try { page = await PublicApi.GetJsonAsync(http, url, Service, ct); }
        catch (PublicApiNotFoundException)
        {
            throw new ArgumentException($"No Wikipedia article titled '{name}'. Try another title.", nameof(title));
        }

        var found = page.GetProperty("title").GetString();
        if (page.TryGetProperty("type", out var type) && type.GetString() == "disambiguation")
            return $"Title '{found}' is ambiguous (disambiguation page). Ask the user which one they mean.";

        var link = page.TryGetProperty("content_urls", out var urls) ? urls.GetProperty("desktop").GetProperty("page").GetString() : url;
        return $"Title: {found}\nURL: {link}\nSummary: {Cut(page.GetProperty("extract").GetString() ?? "")}";
    }

    private static string Cut(string text)
    {
        if (text.Length <= MaxSummary) return text;
        var space = text.LastIndexOf(' ', MaxSummary);
        return text[..(space > 0 ? space : MaxSummary)] + "…";
    }
}
