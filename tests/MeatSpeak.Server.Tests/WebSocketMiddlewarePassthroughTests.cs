using System.Net;
using Xunit;
using NSubstitute;
using MeatSpeak.Server.Core.Server;
using MeatSpeak.Server.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;

namespace MeatSpeak.Server.Tests;

public class WebSocketMiddlewarePassthroughTests
{
    private static WebSocketMiddleware CreateMiddleware(
        RequestDelegate next, string path = "/irc")
    {
        var server = Substitute.For<IServer>();
        var handler = new IrcConnectionHandler(server, NullLogger<IrcConnectionHandler>.Instance, new ServerMetrics());
        var logger = NullLogger<WebSocketMiddleware>.Instance;
        return new WebSocketMiddleware(next, handler, logger, path);
    }

    private static DefaultHttpContext CreateContext(string path, bool isWebSocket = false)
    {
        var context = new DefaultHttpContext();
        context.Request.Path = path;
        context.Connection.RemoteIpAddress = IPAddress.Loopback;
        // DefaultHttpContext.WebSockets.IsWebSocketRequest defaults to false
        return context;
    }

    [Fact]
    public async Task InvokeAsync_NonMatchingPath_PassesThrough()
    {
        bool nextCalled = false;
        var middleware = CreateMiddleware(_ => { nextCalled = true; return Task.CompletedTask; });
        var context = CreateContext("/other");

        await middleware.InvokeAsync(context);

        Assert.True(nextCalled);
    }

    [Fact]
    public async Task InvokeAsync_MatchingPath_NonWebSocket_PassesThrough()
    {
        bool nextCalled = false;
        var middleware = CreateMiddleware(_ => { nextCalled = true; return Task.CompletedTask; });
        var context = CreateContext("/irc");

        // IsWebSocketRequest is false by default
        await middleware.InvokeAsync(context);

        Assert.True(nextCalled);
    }

    [Fact]
    public async Task InvokeAsync_ApiPath_PassesThrough()
    {
        bool nextCalled = false;
        var middleware = CreateMiddleware(_ => { nextCalled = true; return Task.CompletedTask; });
        var context = CreateContext("/api");

        await middleware.InvokeAsync(context);

        Assert.True(nextCalled);
    }

    [Fact]
    public async Task InvokeAsync_AdminPath_PassesThrough()
    {
        bool nextCalled = false;
        var middleware = CreateMiddleware(_ => { nextCalled = true; return Task.CompletedTask; });
        var context = CreateContext("/admin/index.html");

        await middleware.InvokeAsync(context);

        Assert.True(nextCalled);
    }

    [Fact]
    public async Task InvokeAsync_RootPath_PassesThrough()
    {
        bool nextCalled = false;
        var middleware = CreateMiddleware(_ => { nextCalled = true; return Task.CompletedTask; });
        var context = CreateContext("/");

        await middleware.InvokeAsync(context);

        Assert.True(nextCalled);
    }

    [Fact]
    public async Task InvokeAsync_CustomPath_NonMatchingRequest_PassesThrough()
    {
        bool nextCalled = false;
        var middleware = CreateMiddleware(
            _ => { nextCalled = true; return Task.CompletedTask; },
            path: "/ws");
        var context = CreateContext("/irc");

        await middleware.InvokeAsync(context);

        Assert.True(nextCalled);
    }
}
