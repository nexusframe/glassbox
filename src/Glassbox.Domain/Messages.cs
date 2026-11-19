namespace Glassbox.Domain;

public enum Role { System, User, Assistant, Tool }

public sealed record ToolCall(string Id, string Name, string ArgumentsJson);

public sealed record ToolResult(string CallId, string Content, bool IsError);

public sealed record AgentMessage(Role Role, string? Text,
    IReadOnlyList<ToolCall>? ToolCalls = null, ToolResult? ToolResult = null);

public sealed record ToolSpec(string Name, string Description, string InputSchemaJson);

public sealed record ModelUsage(int InputTokens, int OutputTokens);

public sealed record ModelResponse(string? Text, IReadOnlyList<ToolCall> ToolCalls,
    ModelUsage Usage, string ModelId);
