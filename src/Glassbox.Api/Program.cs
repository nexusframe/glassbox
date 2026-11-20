using System.ClientModel;
using System.Net;
using System.Threading.RateLimiting;
using Glassbox.Adapters;
using Glassbox.Api;
using Glassbox.Domain;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using OpenAI;

var builder = WebApplication.CreateBuilder(args);
// Optional runtime override file (kill switch without redeploy).
builder.Configuration.AddJsonFile("/config/glassbox.json", optional: true, reloadOnChange: true);

builder.Services.AddOptions<GlassboxOptions>().BindConfiguration("Glassbox").ValidateOnStart();
builder.Services.AddSingleton<IValidateOptions<GlassboxOptions>, GlassboxOptionsValidator>();

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton(sp =>
{
    var o = sp.GetRequiredService<IOptions<GlassboxOptions>>().Value;
    return new SessionStore(sp.GetRequiredService<TimeProvider>(), TimeSpan.FromMinutes(o.SessionTtlMinutes), o.MaxSessions);
});
builder.Services.AddSingleton<IReadOnlyList<IPNetwork>>(sp =>
    sp.GetRequiredService<IOptions<GlassboxOptions>>().Value.TrustedProxies.Select(IPNetwork.Parse).ToList());
builder.Services.AddSingleton<IBudget>(sp =>
{
    var o = sp.GetRequiredService<IOptions<GlassboxOptions>>().Value;
    return new DailyBudget(new FileBudgetStore(o.BudgetFile), o.DailyBudgetUsd, sp.GetRequiredService<TimeProvider>());
});
builder.Services.AddSingleton<IPriceTable>(sp =>
    new ConfigPriceTable(sp.GetRequiredService<IOptions<GlassboxOptions>>().Value.Prices));
builder.Services.AddSingleton<ICallTool>(sp =>
    new McpToolCaller(new Uri(sp.GetRequiredService<IOptions<GlassboxOptions>>().Value.ToolsUrl)));
builder.Services.AddSingleton<IComplete, LazyOpenRouterCompleter>();

builder.Services.AddCors();
builder.Services.AddOptions<CorsOptions>().Configure<IOptions<GlassboxOptions>>((cors, g) =>
    cors.AddDefaultPolicy(p => p.WithOrigins(g.Value.AllowedOrigins)
        .WithMethods("GET", "POST").WithHeaders("Content-Type").WithExposedHeaders("X-Session-Id")));

builder.Services.AddRateLimiter(rl =>
{
    rl.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    rl.OnRejected = async (ctx, ct) =>
    {
        ctx.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        await ctx.HttpContext.Response.WriteAsJsonAsync(new { error = "rate_limited" }, ct);
    };
    rl.AddPolicy("chat", ctx =>
    {
        var o = ctx.RequestServices.GetRequiredService<IOptionsMonitor<GlassboxOptions>>().CurrentValue;
        var key = ClientIp.Resolve(ctx, ctx.RequestServices.GetRequiredService<IReadOnlyList<IPNetwork>>());
        return RateLimitPartition.GetFixedWindowLimiter(key, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = o.RateLimitPerMinute, Window = TimeSpan.FromMinutes(1), QueueLimit = 0,
        });
    });
});

var app = builder.Build();
{
    var o = app.Services.GetRequiredService<IOptions<GlassboxOptions>>().Value;
    var startupLog = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Glassbox.Startup");
    if (ContentLogging.Enabled(o, app.Environment))
        startupLog.LogWarning("Content logging is ON (Development): prompts, answers and tool data go to the log.");
    else if (o.LogContent)
        startupLog.LogWarning("Glassbox:LogContent is ignored outside Development (environment: {Env}).", app.Environment.EnvironmentName);
}
app.UseCors();
app.UseRateLimiter();
app.MapGet("/health", ChatEndpoint.Health);
app.MapPost("/chat", ChatEndpoint.Post).RequireRateLimiting("chat");
app.Run();

public partial class Program
{
    public const string KeyName = "OPENROUTER_API_KEY";
}

/// <summary>Builds the real model client on first use, so the app starts without a key.</summary>
public sealed class LazyOpenRouterCompleter(IConfiguration config, IOptionsMonitor<GlassboxOptions> options) : IComplete
{
    private readonly object _gate = new();
    private IComplete? _inner;

    public Task<ModelResponse> CompleteAsync(IReadOnlyList<AgentMessage> messages,
        IReadOnlyList<ToolSpec> tools, Func<string, ValueTask> onTextDelta, CancellationToken ct) =>
        Inner().CompleteAsync(messages, tools, onTextDelta, ct);

    private IComplete Inner()
    {
        lock (_gate)
        {
            if (_inner is not null) return _inner;
            var key = config[Program.KeyName];
            if (string.IsNullOrWhiteSpace(key)) throw new InvalidOperationException("OPENROUTER_API_KEY is not set.");
            var o = options.CurrentValue;
            IChatClient client = new OpenAIClient(new ApiKeyCredential(key),
                    new OpenAIClientOptions { Endpoint = new Uri(o.ModelBaseUrl) })
                .GetChatClient(o.ModelId).AsIChatClient();
            return _inner = new RetryOnceCompleter(new ChatClientCompleter(client, o.ModelId, o.MaxOutputTokens));
        }
    }
}

public sealed class GlassboxOptionsValidator : IValidateOptions<GlassboxOptions>
{
    public ValidateOptionsResult Validate(string? name, GlassboxOptions o) =>
        string.IsNullOrWhiteSpace(o.ModelId)
            ? ValidateOptionsResult.Fail("Glassbox:ModelId is not set.")
            : o.Prices.ContainsKey(o.ModelId) ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail($"Glassbox:Prices has no entry for model '{o.ModelId}'. Add its prices before starting.");
}
