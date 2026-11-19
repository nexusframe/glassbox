using Glassbox.Tools.Mcp;
using Microsoft.Extensions.Time.Testing;

namespace Glassbox.Tests;

public class DateTimeToolTests
{
    private static FakeTimeProvider Clock() => new(DateTimeOffset.Parse("2026-11-15T14:14:00Z"));

    [Theory]
    [InlineData("Asia/Tokyo", "2026-11-15 23:14, Sunday, UTC+09:00 (Asia/Tokyo)")]
    [InlineData("UTC", "2026-11-15 14:14, Sunday, UTC+00:00 (UTC)")]
    public void Formats_time_in_zone(string zone, string expected) =>
        Assert.Equal(expected, DateTimeTool.Now(zone, Clock()));

    [Fact]
    public void Unknown_zone_throws_with_iana_hint()
    {
        var ex = Assert.Throws<ArgumentException>(() => DateTimeTool.Now("Mars/Base", Clock()));
        Assert.Contains("IANA", ex.Message);
    }
}
