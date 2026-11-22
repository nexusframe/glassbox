namespace Glassbox.Tools.Mcp;

public static class ToolsServer
{
    public static WebApplication Build(string[] args, string url)
    {
        var builder = WebApplication.CreateBuilder(args);
        builder.WebHost.UseUrls(url);
        builder.Logging.AddFilter("ModelContextProtocol", LogLevel.Warning);
        builder.Services.AddSingleton(TimeProvider.System);
        builder.Services.AddHttpClient(GlassboxTools.HttpClientName, c =>
        {
            c.Timeout = TimeSpan.FromSeconds(5);
            c.MaxResponseContentBufferSize = 1_000_000;
            // Wikimedia asks for a descriptive User-Agent; the repo URL holds no personal data.
            c.DefaultRequestHeaders.UserAgent.ParseAdd("glassbox-demo/1.0 (+https://github.com/nexusframe/glassbox)");
        });
        builder.Services.AddMcpServer().WithHttpTransport().WithTools<GlassboxTools>();

        var app = builder.Build();
        app.MapMcp("/mcp");
        app.MapGet("/health", () => Results.Ok());
        return app;
    }
}
