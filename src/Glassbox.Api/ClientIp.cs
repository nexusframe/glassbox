using System.Net;
using Microsoft.AspNetCore.Http;

namespace Glassbox.Api;

public static class ClientIp
{
    public static string Resolve(HttpContext ctx, IReadOnlyList<IPNetwork> trustedProxies)
    {
        var remote = Normalize(ctx.Connection.RemoteIpAddress);
        if (remote is null) return "unknown";
        if (trustedProxies.Any(n => n.Contains(remote))
            && ctx.Request.Headers["CF-Connecting-IP"].ToString() is { Length: > 0 } header
            && IPAddress.TryParse(header.Trim(), out var client))
            return Normalize(client)!.ToString();
        return remote.ToString();
    }

    private static IPAddress? Normalize(IPAddress? ip) =>
        ip is { IsIPv4MappedToIPv6: true } ? ip.MapToIPv4() : ip;
}
