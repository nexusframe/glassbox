using System.Net;
using Glassbox.Tests.Fakes;
using Glassbox.Tools.Mcp;

namespace Glassbox.Tests;

public class WikiToolTests
{
    private static string Page(string extract = "Kraków – miasto na prawach powiatu w południowej Polsce.", string type = "standard") =>
        System.Text.Json.JsonSerializer.Serialize(new
        {
            type, title = "Kraków", extract,
            content_urls = new { desktop = new { page = "https://pl.wikipedia.org/wiki/Krak%C3%B3w" } },
        });

    private static StubHttpHandler Stub(string body) => new(_ => StubHttpHandler.Json(body));

    private static Task<string> Summary(StubHttpHandler stub, string title = "Kraków", string language = "pl") =>
        WikiTool.SummaryAsync(title, language, stub.Client(), CancellationToken.None);

    [Fact]
    public async Task Formats_summary() =>
        Assert.Equal(
            "Title: Kraków\nURL: https://pl.wikipedia.org/wiki/Krak%C3%B3w\nSummary: Kraków – miasto na prawach powiatu w południowej Polsce.",
            await Summary(Stub(Page())));

    [Fact]
    public async Task Builds_url_with_underscores()
    {
        var stub = Stub(Page());
        await Summary(stub, "Maria Skłodowska-Curie");
        Assert.Equal("pl.wikipedia.org", stub.Requests[0].Host);
        Assert.EndsWith("/api/rest_v1/page/summary/Maria_Sk%C5%82odowska-Curie", stub.Requests[0].AbsoluteUri);
    }

    [Fact]
    public async Task Long_extract_is_cut_at_800_chars()
    {
        var words = string.Join(' ', Enumerable.Repeat("słowo", 400)); // 2399 chars
        var result = await Summary(Stub(Page(words)));
        var summary = result[(result.IndexOf("Summary: ", StringComparison.Ordinal) + 9)..];
        Assert.True(summary.Length <= 801, $"length {summary.Length}");
        Assert.EndsWith("słowo…", summary);
    }

    [Fact]
    public async Task Disambiguation_page_says_ambiguous()
    {
        var result = await Summary(Stub(Page(type: "disambiguation")));
        Assert.Contains("ambiguous", result);
        Assert.Contains("Ask the user", result);
    }

    [Fact]
    public async Task Missing_article_throws()
    {
        var stub = new StubHttpHandler(_ => StubHttpHandler.Json("{}", HttpStatusCode.NotFound));
        var ex = await Assert.ThrowsAsync<ArgumentException>(() => Summary(stub, "Nie ma takiego"));
        Assert.Contains("No Wikipedia article", ex.Message);
    }

    [Fact]
    public async Task Unsupported_language_throws()
    {
        var stub = Stub(Page());
        var ex = await Assert.ThrowsAsync<ArgumentException>(() => Summary(stub, language: "de"));
        Assert.Contains("pl or en", ex.Message);
        Assert.Empty(stub.Requests);
    }

    [Fact]
    public async Task Page_without_extract_throws()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() => Summary(Stub(Page("", "no-extract"))));
        Assert.Contains("has no summary", ex.Message);
        await Assert.ThrowsAsync<ArgumentException>(() => Summary(Stub("""{"type":"standard","title":"Kraków"}""")));
    }

    [Fact]
    public async Task Missing_desktop_url_falls_back_to_request_url()
    {
        var result = await Summary(Stub("""{"type":"standard","title":"Kraków","extract":"Miasto.","content_urls":{}}"""));
        Assert.Contains("URL: https://pl.wikipedia.org/api/rest_v1/page/summary/Krak%C3%B3w", result);
    }

    [Fact]
    public async Task Invalid_title_rejected_by_wikipedia_is_an_input_error()
    {
        var stub = new StubHttpHandler(_ => StubHttpHandler.Json("{}", HttpStatusCode.BadRequest));
        var ex = await Assert.ThrowsAsync<ArgumentException>(() => Summary(stub, "[x]"));
        Assert.Contains("rejected", ex.Message);
    }
}
