using System.Runtime.CompilerServices;
using System.Text.Json;
using Glassbox.Adapters;
using Glassbox.Domain;
using Microsoft.Extensions.AI;

namespace Glassbox.Tests;

public class ChatClientCompleterTests
{
    private sealed class FakeChatClient(params ChatResponseUpdate[] updates) : IChatClient
    {
        public List<ChatMessage> Messages { get; private set; } = [];
        public ChatOptions? Options { get; private set; }

        public Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(IEnumerable<ChatMessage> messages,
            ChatOptions? options = null, [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            Messages = messages.ToList();
            Options = options;
            foreach (var u in updates) { await Task.Yield(); yield return u; }
        }

        public object? GetService(Type serviceType, object? serviceKey = null) => null;
        public void Dispose() { }
    }

    private static ChatResponseUpdate Text(string t, string? model = null) =>
        new(ChatRole.Assistant, t) { ModelId = model };

    private static readonly ToolSpec Calc = new("calculate", "Math",
        """{"type":"object","properties":{"expression":{"type":"string"}},"required":["expression"]}""");

    private static Task<ModelResponse> Run(FakeChatClient c, IReadOnlyList<AgentMessage> msgs,
        List<string>? deltas = null, IReadOnlyList<ToolSpec>? tools = null) =>
        new ChatClientCompleter(c, "cfg-model", 600).CompleteAsync(msgs, tools ?? [Calc],
            d => { deltas?.Add(d); return ValueTask.CompletedTask; }, CancellationToken.None);

    [Fact]
    public async Task Text_deltas_stream_in_order_and_are_joined()
    {
        var c = new FakeChatClient(Text("Hel"), Text("lo"), Text("!"));
        var deltas = new List<string>();
        var r = await Run(c, [new(Role.User, "hi")], deltas);
        Assert.Equal(["Hel", "lo", "!"], deltas);
        Assert.Equal("Hello!", r.Text);
        Assert.Empty(r.ToolCalls);
    }

    [Fact]
    public async Task Function_call_becomes_tool_call_with_same_id_name_and_json()
    {
        var upd = new ChatResponseUpdate(ChatRole.Assistant,
            [new FunctionCallContent("call_1", "calculate", new Dictionary<string, object?> { ["expression"] = "240*0.17" })]);
        var r = await Run(new FakeChatClient(upd), [new(Role.User, "x")]);
        var call = Assert.Single(r.ToolCalls);
        Assert.Equal("call_1", call.Id);
        Assert.Equal("calculate", call.Name);
        using var doc = JsonDocument.Parse(call.ArgumentsJson);
        Assert.Equal("240*0.17", doc.RootElement.GetProperty("expression").GetString());
    }

    [Fact]
    public async Task Function_call_without_arguments_gives_empty_object()
    {
        var upd = new ChatResponseUpdate(ChatRole.Assistant, [new FunctionCallContent("c", "get_datetime", null)]);
        var r = await Run(new FakeChatClient(upd), [new(Role.User, "x")]);
        Assert.Equal("{}", r.ToolCalls[0].ArgumentsJson);
    }

    [Fact]
    public async Task Usage_content_becomes_model_usage()
    {
        var upd = new ChatResponseUpdate(ChatRole.Assistant,
            [new UsageContent(new UsageDetails { InputTokenCount = 12, OutputTokenCount = 7 })]);
        var r = await Run(new FakeChatClient(Text("a"), upd), [new(Role.User, "x")]);
        Assert.Equal(new ModelUsage(12, 7), r.Usage);
    }

    [Fact]
    public async Task Options_carry_max_tokens_model_and_tool_declarations()
    {
        var c = new FakeChatClient(Text("a"));
        await Run(c, [new(Role.User, "x")]);
        Assert.Equal(600, c.Options!.MaxOutputTokens);
        Assert.Equal("cfg-model", c.Options.ModelId);
        var tool = Assert.Single(c.Options.Tools!);
        Assert.Equal("calculate", tool.Name);
        Assert.IsNotAssignableFrom<AIFunction>(tool); // declaration only, cannot be invoked here
        var decl = Assert.IsAssignableFrom<AIFunctionDeclaration>(tool);
        Assert.Equal("object", decl.JsonSchema.GetProperty("type").GetString());
    }

    [Fact]
    public async Task Messages_map_roles_tool_calls_and_tool_results()
    {
        var c = new FakeChatClient(Text("a"));
        await Run(c,
        [
            new(Role.System, "sys"),
            new(Role.User, "q"),
            new(Role.Assistant, null, [new ToolCall("id1", "calculate", """{"expression":"1+1"}""")]),
            new(Role.Tool, null, null, new ToolResult("id1", "2", false)),
        ]);
        Assert.Equal(4, c.Messages.Count);
        Assert.Equal(ChatRole.System, c.Messages[0].Role);
        Assert.Equal("sys", c.Messages[0].Text);
        Assert.Equal(ChatRole.User, c.Messages[1].Role);
        var fc = Assert.IsType<FunctionCallContent>(Assert.Single(c.Messages[2].Contents));
        Assert.Equal(ChatRole.Assistant, c.Messages[2].Role);
        Assert.Equal("id1", fc.CallId);
        Assert.Equal("calculate", fc.Name);
        Assert.Equal("1+1", fc.Arguments!["expression"]?.ToString());
        Assert.Equal(ChatRole.Tool, c.Messages[3].Role);
        var fr = Assert.IsType<FunctionResultContent>(Assert.Single(c.Messages[3].Contents));
        Assert.Equal("id1", fr.CallId);
        Assert.Equal("2", fr.Result?.ToString());
    }

    [Fact]
    public async Task Invalid_argument_json_from_history_does_not_throw()
    {
        var c = new FakeChatClient(Text("a"));
        await Run(c, [new(Role.Assistant, null, [new ToolCall("i", "calculate", "{broken")])]);
        Assert.IsType<FunctionCallContent>(c.Messages[0].Contents[0]);
    }

    [Fact]
    public async Task Model_id_is_always_the_configured_one()
    {
        // The price table is keyed by the configured id; a served id like "x/model-2026..." must not leak in.
        var r1 = await Run(new FakeChatClient(Text("a", "served-model")), [new(Role.User, "x")]);
        Assert.Equal("cfg-model", r1.ModelId);
        var r2 = await Run(new FakeChatClient(Text("a")), [new(Role.User, "x")]);
        Assert.Equal("cfg-model", r2.ModelId);
    }
}
