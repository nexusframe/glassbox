using System.Globalization;

namespace Glassbox.Tools.Mcp;

public static class DateTimeTool
{
    public static string Now(string timeZone, TimeProvider time)
    {
        TimeZoneInfo zone;
        try { zone = TimeZoneInfo.FindSystemTimeZoneById(timeZone); }
        catch (Exception e) when (e is TimeZoneNotFoundException or InvalidTimeZoneException or ArgumentException)
        {
            throw new ArgumentException($"Unknown time zone '{timeZone}'. Use an IANA id such as Europe/Warsaw.", nameof(timeZone));
        }
        var local = TimeZoneInfo.ConvertTime(time.GetUtcNow(), zone);
        var o = local.Offset;
        var sign = o < TimeSpan.Zero ? "-" : "+";
        var offset = $"{sign}{o.Duration().Hours:00}:{o.Duration().Minutes:00}";
        return string.Create(CultureInfo.InvariantCulture,
            $"{local:yyyy-MM-dd HH:mm}, {local.DayOfWeek}, UTC{offset} ({timeZone})");
    }
}
