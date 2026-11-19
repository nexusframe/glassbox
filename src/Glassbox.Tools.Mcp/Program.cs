using Glassbox.Tools.Mcp;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls("http://+:8081");
builder.Logging.AddFilter("ModelContextProtocol", LogLevel.Warning);
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddMcpServer().WithHttpTransport().WithTools<GlassboxTools>();

var app = builder.Build();
app.MapMcp("/mcp");
app.MapGet("/health", () => Results.Ok());
app.Run();
