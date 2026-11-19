using System.ComponentModel;
using ModelContextProtocol;
using ModelContextProtocol.Server;

namespace Glassbox.Tools.Mcp;

[McpServerToolType]
public sealed class GlassboxTools(TimeProvider time)
{
    [McpServerTool(Name = "calculate"), Description("Evaluates plain arithmetic with + - * / and parentheses, e.g. 240*0.17. Use a dot as decimal separator. No variables or functions.")]
    public string Calculate([Description("Arithmetic expression, e.g. (2+3)*4")] string expression)
    {
        try { return Calculator.Evaluate(expression).ToString(System.Globalization.CultureInfo.InvariantCulture); }
        catch (FormatException e) { throw new McpException(e.Message); }
    }

    [McpServerTool(Name = "get_datetime"), Description("Returns the current date and time in a time zone. Use an IANA zone id such as Europe/Warsaw or Asia/Tokyo.")]
    public string GetDateTime([Description("IANA time zone id, e.g. Europe/Warsaw")] string timeZone = "UTC")
    {
        try { return DateTimeTool.Now(timeZone, time); }
        catch (ArgumentException e) { throw new McpException(e.Message); }
    }
}
