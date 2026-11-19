extern alias api;
using System.Net;
using api::Glassbox.Api;
using Microsoft.AspNetCore.Http;

namespace Glassbox.Tests;

public class ClientIpTests
{
    private static readonly IReadOnlyList<IPNetwork> Trusted = [IPNetwork.Parse("172.30.0.0/24")];

    private static DefaultHttpContext Ctx(string remote, string? header)
    {
        var c = new DefaultHttpContext();
        c.Connection.RemoteIpAddress = IPAddress.Parse(remote);
        if (header is not null) c.Request.Headers["CF-Connecting-IP"] = header;
        return c;
    }

    [Fact]
    public void Trusted_proxy_header_is_used() =>
        Assert.Equal("203.0.113.7", ClientIp.Resolve(Ctx("172.30.0.10", "203.0.113.7"), Trusted));

    [Fact]
    public void Untrusted_remote_ignores_header() =>
        Assert.Equal("198.51.100.1", ClientIp.Resolve(Ctx("198.51.100.1", "203.0.113.7"), Trusted));

    [Fact]
    public void Trusted_remote_without_header_uses_remote() =>
        Assert.Equal("172.30.0.10", ClientIp.Resolve(Ctx("172.30.0.10", null), Trusted));

    [Fact]
    public void Invalid_header_from_trusted_proxy_uses_remote() =>
        Assert.Equal("172.30.0.10", ClientIp.Resolve(Ctx("172.30.0.10", "not-an-ip"), Trusted));

    [Fact]
    public void Ipv4_mapped_remote_matches_ipv4_network() =>
        Assert.Equal("203.0.113.7", ClientIp.Resolve(Ctx("::ffff:172.30.0.10", "203.0.113.7"), Trusted));

    [Fact]
    public void No_trusted_proxies_means_header_is_ignored() =>
        Assert.Equal("172.30.0.10", ClientIp.Resolve(Ctx("172.30.0.10", "203.0.113.7"), []));
}
