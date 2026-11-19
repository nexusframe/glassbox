namespace Glassbox.Tools.Mcp;

public static class ToolsServer
{
    public static WebApplication Build(string[] args, string url)
    {
        var builder = WebApplication.CreateBuilder(args);
        builder.WebHost.UseUrls(url);
        builder.Logging.AddFilter("ModelContextProtocol", LogLevel.Warning);
        builder.Services.AddSingleton(TimeProvider.System);
        builder.Services.AddMcpServer().WithHttpTransport().WithTools<GlassboxTools>();

        var app = builder.Build();
        app.MapMcp("/mcp");
        app.MapGet("/health", () => Results.Ok());
        return app;
    }
}
