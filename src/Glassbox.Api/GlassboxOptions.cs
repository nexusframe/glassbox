using Glassbox.Adapters;

namespace Glassbox.Api;

public sealed class GlassboxOptions
{
    public string ModelBaseUrl { get; set; } = "https://openrouter.ai/api/v1";
    public string ModelId { get; set; } = "";
    public int MaxOutputTokens { get; set; } = 600;
    public string ToolsUrl { get; set; } = "http://tools:8081/mcp";
    public decimal DailyBudgetUsd { get; set; } = 0.10m;
    public string BudgetFile { get; set; } = "/data/budget.json";
    public int MaxTurns { get; set; } = 5;
    public int MaxToolCallsPerTurn { get; set; } = 4;
    public int MaxRunSeconds { get; set; } = 60;
    public int MaxInputChars { get; set; } = 500;
    public int SessionTtlMinutes { get; set; } = 30;
    public int MaxSessions { get; set; } = 200;
    public int RateLimitPerMinute { get; set; } = 10;
    public string[] AllowedOrigins { get; set; } = [];
    public string[] TrustedProxies { get; set; } = [];
    public bool ModelCallsEnabled { get; set; } = true;
    public Dictionary<string, ModelPrice> Prices { get; set; } = [];
}
