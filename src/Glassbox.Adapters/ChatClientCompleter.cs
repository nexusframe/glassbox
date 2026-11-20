using System.Text;
using System.Text.Json;
using Glassbox.Domain;
using Microsoft.Extensions.AI;
using OpenAI.Chat;
using ChatMessage = Microsoft.Extensions.AI.ChatMessage;

namespace Glassbox.Adapters;

/// <summary>
/// Streams one model turn through an IChatClient. Tools are sent as declarations only;
/// this class never invokes them (the agent loop does).
/// </summary>
public sealed class ChatClientCompleter(IChatClient client, string modelId, int maxOutputTokens) : IComplete
{
    public async Task<ModelResponse> CompleteAsync(IReadOnlyList<AgentMessage> messages,
        IReadOnlyList<ToolSpec> tools, Func<string, ValueTask> onTextDelta, CancellationToken ct)
    {
        var options = new ChatOptions
        {
            ModelId = modelId,
            MaxOutputTokens = maxOutputTokens,
            Tools = tools.Select(ToDeclaration).ToList(),
        };

        var text = new StringBuilder();
        var calls = new List<ToolCall>();
        int input = 0, output = 0;
        // M.E.AI drops the argument text when it is not valid JSON (Arguments = null, Exception set),
        // so keep the raw fragments from the OpenAI wire updates, keyed by stream index and call id.
        var rawByIndex = new Dictionary<int, StringBuilder>();
        var indexById = new Dictionary<string, int>();
        var unparsed = new List<(int Position, string CallId)>();

        await foreach (var update in client.GetStreamingResponseAsync(messages.Select(ToChatMessage).ToList(), options, ct))
        {
            if (update.RawRepresentation is StreamingChatCompletionUpdate raw)
                foreach (var tc in raw.ToolCallUpdates)
                {
                    if (!rawByIndex.TryGetValue(tc.Index, out var sb)) rawByIndex[tc.Index] = sb = new StringBuilder();
                    sb.Append(tc.FunctionArgumentsUpdate?.ToString());
                    if (!string.IsNullOrEmpty(tc.ToolCallId)) indexById[tc.ToolCallId] = tc.Index;
                }

            foreach (var content in update.Contents)
            {
                switch (content)
                {
                    case TextContent t when !string.IsNullOrEmpty(t.Text):
                        text.Append(t.Text);
                        await onTextDelta(t.Text);
                        break;
                    case FunctionCallContent f:
                        if (f.Exception is not null && f.Arguments is null) unparsed.Add((calls.Count, f.CallId));
                        calls.Add(new ToolCall(f.CallId, f.Name, ArgsToJson(f.Arguments)));
                        break;
                    case UsageContent u:
                        input += (int)(u.Details.InputTokenCount ?? 0);
                        output += (int)(u.Details.OutputTokenCount ?? 0);
                        break;
                }
            }
        }

        foreach (var (pos, callId) in unparsed)
            if (indexById.TryGetValue(callId, out var idx) && rawByIndex.TryGetValue(idx, out var rawText) && rawText.Length > 0)
                calls[pos] = calls[pos] with { ArgumentsJson = rawText.ToString() };

        // Always the configured id: the price table is keyed by it, and a provider may
        // report a different (e.g. dated) id in the response.
        return new ModelResponse(text.Length == 0 ? null : text.ToString(), calls,
            new ModelUsage(input, output), modelId);
    }

    private static AITool ToDeclaration(ToolSpec spec)
    {
        JsonElement schema;
        try { schema = JsonDocument.Parse(spec.InputSchemaJson).RootElement.Clone(); }
        catch (JsonException) { schema = JsonDocument.Parse("""{"type":"object"}""").RootElement.Clone(); }
        return AIFunctionFactory.CreateDeclaration(spec.Name, spec.Description, schema);
    }

    private static ChatMessage ToChatMessage(AgentMessage m)
    {
        switch (m.Role)
        {
            case Role.System: return new ChatMessage(ChatRole.System, m.Text ?? "");
            case Role.User: return new ChatMessage(ChatRole.User, m.Text ?? "");
            case Role.Tool:
                return new ChatMessage(ChatRole.Tool,
                    [new FunctionResultContent(m.ToolResult?.CallId ?? "", m.ToolResult?.Content ?? "")]);
            default:
                var contents = new List<AIContent>();
                if (!string.IsNullOrEmpty(m.Text)) contents.Add(new TextContent(m.Text));
                foreach (var c in m.ToolCalls ?? [])
                    contents.Add(new FunctionCallContent(c.Id, c.Name, JsonToArgs(c.ArgumentsJson)));
                return new ChatMessage(ChatRole.Assistant, contents);
        }
    }

    private static string ArgsToJson(IDictionary<string, object?>? args) =>
        args is null or { Count: 0 } ? "{}" : JsonSerializer.Serialize(args);

    // Invalid JSON is kept out of the typed dictionary; the tool server rejects the call later.
    private static Dictionary<string, object?>? JsonToArgs(string json)
    {
        try { return JsonSerializer.Deserialize<Dictionary<string, object?>>(json); }
        catch (JsonException) { return null; }
    }
}
