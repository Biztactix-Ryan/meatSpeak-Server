using System.Net;
using Xunit;
using NSubstitute;
using MeatSpeak.Server.Admin;
using MeatSpeak.Server.Core.Server;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace MeatSpeak.Server.Tests;

public class IpAllowListMiddlewareTests
{
    private static IpAllowListMiddleware CreateMiddleware(
        RequestDelegate next, List<string>? allowList = null)
    {
        var config = new ServerConfig();
        if (allowList != null)
            config.AdminApi.IpAllowList = allowList;
        var logger = NullLogger<IpAllowListMiddleware>.Instance;
        return new IpAllowListMiddleware(next, config, logger);
    }

    private static DefaultHttpContext CreateContext(string path, string remoteIp)
    {
        var context = new DefaultHttpContext();
        context.Request.Path = path;
        context.Connection.RemoteIpAddress = IPAddress.Parse(remoteIp);
        return context;
    }

    [Fact]
    public async Task IrcPath_AlwaysAllowed()
    {
        bool nextCalled = false;
        var middleware = CreateMiddleware(_ => { nextCalled = true; return Task.CompletedTask; });
        var context = CreateContext("/irc", "1.2.3.4");

        await middleware.InvokeAsync(context);

        Assert.True(nextCalled);
    }

    [Fact]
    public async Task ApiPath_LocalhostAllowed_ByDefault()
    {
        bool nextCalled = false;
        var middleware = CreateMiddleware(_ => { nextCalled = true; return Task.CompletedTask; });
        var context = CreateContext("/api", "127.0.0.1");

        await middleware.InvokeAsync(context);

        Assert.True(nextCalled);
    }

    [Fact]
    public async Task ApiPath_RemoteIpDenied_ByDefault()
    {
        bool nextCalled = false;
        var middleware = CreateMiddleware(_ => { nextCalled = true; return Task.CompletedTask; });
        var context = CreateContext("/api", "10.0.0.5");

        await middleware.InvokeAsync(context);

        Assert.False(nextCalled);
        Assert.Equal(403, context.Response.StatusCode);
    }

    [Fact]
    public async Task AdminPath_RemoteIpDenied_ByDefault()
    {
        bool nextCalled = false;
        var middleware = CreateMiddleware(_ => { nextCalled = true; return Task.CompletedTask; });
        var context = CreateContext("/admin/index.html", "192.168.1.1");

        await middleware.InvokeAsync(context);

        Assert.False(nextCalled);
        Assert.Equal(403, context.Response.StatusCode);
    }

    [Fact]
    public async Task ApiPath_AllowedCidr_Passes()
    {
        bool nextCalled = false;
        var middleware = CreateMiddleware(
            _ => { nextCalled = true; return Task.CompletedTask; },
            new List<string> { "10.0.0.0/8" });
        var context = CreateContext("/api", "10.50.100.200");

        await middleware.InvokeAsync(context);

        Assert.True(nextCalled);
    }

    [Fact]
    public async Task ApiPath_OutsideCidr_Denied()
    {
        bool nextCalled = false;
        var middleware = CreateMiddleware(
            _ => { nextCalled = true; return Task.CompletedTask; },
            new List<string> { "10.0.0.0/8" });
        var context = CreateContext("/api", "192.168.1.1");

        await middleware.InvokeAsync(context);

        Assert.False(nextCalled);
        Assert.Equal(403, context.Response.StatusCode);
    }

    [Fact]
    public async Task Ipv6Loopback_AllowedByDefault()
    {
        bool nextCalled = false;
        var middleware = CreateMiddleware(_ => { nextCalled = true; return Task.CompletedTask; });
        var context = CreateContext("/api", "::1");

        await middleware.InvokeAsync(context);

        Assert.True(nextCalled);
    }

    [Fact]
    public async Task NonProtectedPath_AlwaysPasses()
    {
        bool nextCalled = false;
        var middleware = CreateMiddleware(_ => { nextCalled = true; return Task.CompletedTask; });
        var context = CreateContext("/other", "1.2.3.4");

        await middleware.InvokeAsync(context);

        Assert.True(nextCalled);
    }
}
