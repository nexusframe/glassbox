using Glassbox.Adapters;
using Glassbox.Domain;
using Glassbox.Tools.Mcp;
using Microsoft.AspNetCore.Builder;

namespace Glassbox.Tests;

public class McpToolCallerTests
{
    private static async Task<(WebApplication App, Uri Endpoint)> StartAsync(int port = 0)
    {
        var app = ToolsServer.Build([], $"http://127.0.0.1:{port}");
        await app.StartAsync();
        var uri = new Uri(app.Urls.First());
        return (app, new Uri($"http://127.0.0.1:{uri.Port}/mcp"));
    }

    private static ToolCall Call(string name, string json) => new("c1", name, json);

    [Fact]
    public async Task Lists_both_tools_with_schemas()
    {
        var (app, ep) = await StartAsync();
        await using var _ = app;
        await using var caller = new McpToolCaller(ep, TimeSpan.FromSeconds(10));
        var tools = await caller.ListToolsAsync(CancellationToken.None);
        Assert.Contains(tools, t => t.Name == "calculate");
        Assert.Contains(tools, t => t.Name == "get_datetime");
        Assert.All(tools, t => { Assert.NotEmpty(t.Description); Assert.Contains("object", t.InputSchemaJson); });
    }

    [Fact]
    public async Task Calculate_returns_plain_invariant_number()
    {
        var (app, ep) = await StartAsync();
        await using var _ = app;
        await using var caller = new McpToolCaller(ep, TimeSpan.FromSeconds(10));
        var r = await caller.CallAsync(Call("calculate", """{"expression":"240*0.17"}"""), CancellationToken.None);
        Assert.False(r.IsError);
        Assert.Equal("40.80", r.Content);
        Assert.Equal("c1", r.CallId);
        Assert.Equal("2.5", (await caller.CallAsync(Call("calculate", """{"expression":"10/4"}"""), default)).Content);
        Assert.Equal("14", (await caller.CallAsync(Call("calculate", """{"expression":"2+3*4"}"""), default)).Content);
        Assert.Equal("-2", (await caller.CallAsync(Call("calculate", """{"expression":"3-5"}"""), default)).Content);
    }

    [Fact]
    public async Task Tool_error_is_reported_with_server_text()
    {
        var (app, ep) = await StartAsync();
        await using var _ = app;
        await using var caller = new McpToolCaller(ep, TimeSpan.FromSeconds(10));
        var r = await caller.CallAsync(Call("calculate", """{"expression":"1/0"}"""), CancellationToken.None);
        Assert.True(r.IsError);
        Assert.Contains("zero", r.Content, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Unknown_tool_is_error_without_exception()
    {
        var (app, ep) = await StartAsync();
        await using var _ = app;
        await using var caller = new McpToolCaller(ep, TimeSpan.FromSeconds(10));
        var r = await caller.CallAsync(Call("nope", "{}"), CancellationToken.None);
        Assert.True(r.IsError);
    }

    [Fact]
    public async Task Invalid_argument_json_is_error_without_exception()
    {
        var (app, ep) = await StartAsync();
        await using var _ = app;
        await using var caller = new McpToolCaller(ep, TimeSpan.FromSeconds(10));
        var r = await caller.CallAsync(Call("calculate", "{\"expression\":\"100*(1+0.05/12"), CancellationToken.None);
        Assert.True(r.IsError);
        Assert.Equal("Invalid tool arguments: not valid JSON.", r.Content);
    }

    [Fact]
    public async Task Endpoint_down_gives_generic_failure()
    {
        // port 1 is closed on loopback: connection refused
        await using var caller = new McpToolCaller(new Uri("http://127.0.0.1:1/mcp"), TimeSpan.FromSeconds(2));
        var r = await caller.CallAsync(Call("calculate", """{"expression":"1+1"}"""), CancellationToken.None);
        Assert.True(r.IsError);
        Assert.Equal("Tool failed.", r.Content);
    }

    [Fact]
    public async Task Cancelled_token_throws_operation_canceled()
    {
        var (app, ep) = await StartAsync();
        await using var _ = app;
        await using var caller = new McpToolCaller(ep, TimeSpan.FromSeconds(10));
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => caller.CallAsync(Call("calculate", """{"expression":"1+1"}"""), cts.Token));
    }

    [Fact]
    public async Task Recovers_after_server_restart()
    {
        var (app, ep) = await StartAsync();
        await using var caller = new McpToolCaller(ep, TimeSpan.FromSeconds(10));
        Assert.False((await caller.CallAsync(Call("calculate", """{"expression":"1+1"}"""), default)).IsError);
        await app.StopAsync();
        await app.DisposeAsync();

        var (app2, _) = await StartAsync(ep.Port);
        await using var __ = app2;
        var r = await caller.CallAsync(Call("calculate", """{"expression":"240*0.17"}"""), default);
        Assert.False(r.IsError);
        Assert.Equal("40.80", r.Content);
    }

    [Fact]
    public async Task Parallel_calls_share_one_connection_safely()
    {
        var (app, ep) = await StartAsync();
        await using var _ = app;
        await using var caller = new McpToolCaller(ep, TimeSpan.FromSeconds(10));
        var rs = await Task.WhenAll(Enumerable.Range(1, 8).Select(i =>
            caller.CallAsync(new ToolCall($"c{i}", "calculate", $$"""{"expression":"{{i}}+1"}"""), default)));
        Assert.All(rs, r => Assert.False(r.IsError));
        Assert.Equal(Enumerable.Range(2, 8).Select(i => i.ToString()), rs.Select(r => r.Content));
    }
}
