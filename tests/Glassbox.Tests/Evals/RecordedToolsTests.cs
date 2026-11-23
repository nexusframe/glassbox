using Glassbox.Domain;
using Glassbox.Evals;
using Glassbox.Tests.Fakes;

namespace Glassbox.Tests.Evals;

public class RecordedToolsTests
{
    private static readonly Recording[] Weather =
    [
        new("get_weather", "Warsz|Warsaw", "W: 3.5°C"),
        new("get_weather", "Rzym|Rome|Roma", "R: 14°C"),
    ];

    private static ToolCall Call(string name, string json) => new("c1", name, json);

    [Fact]
    public async Task Lists_real_specs()
    {
        var real = new FakeTools().Returns("calculate").Returns("get_weather");
        var specs = await new RecordedTools(real, []).ListToolsAsync(CancellationToken.None);
        Assert.Equal(["calculate", "get_weather"], specs.Select(s => s.Name));
    }

    [Fact]
    public async Task Calculate_goes_to_real_tool()
    {
        var real = new FakeTools().Returns("calculate", "40.80");
        var r = await new RecordedTools(real, []).CallAsync(Call("calculate", """{"expression":"240*0.17"}"""), CancellationToken.None);
        Assert.Equal("40.80", r.Content);
        Assert.Single(real.Executed);
    }

    [Fact]
    public async Task Picks_recording_by_args_pattern()
    {
        var real = new FakeTools();
        var r = await new RecordedTools(real, Weather).CallAsync(Call("get_weather", """{"city":"Rome"}"""), CancellationToken.None);
        Assert.Equal(new ToolResult("c1", "R: 14°C", false), r);
        Assert.Empty(real.Executed);
    }

    [Fact]
    public async Task Null_pattern_matches_any_args()
    {
        var tools = new RecordedTools(new FakeTools(), [new("wiki_summary", null, "Title: X")]);
        var r = await tools.CallAsync(Call("wiki_summary", """{"title":"anything"}"""), CancellationToken.None);
        Assert.Equal(new ToolResult("c1", "Title: X", false), r);
    }

    [Fact]
    public async Task Unmatched_call_returns_error_result()
    {
        var r = await new RecordedTools(new FakeTools(), Weather).CallAsync(Call("get_weather", """{"city":"Oslo"}"""), CancellationToken.None);
        Assert.Equal(new ToolResult("c1", "No recording for this call.", true), r);
    }
}
