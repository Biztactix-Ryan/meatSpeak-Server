namespace MeatSpeak.Server;

using System.Net;
using MeatSpeak.Server.Transport.WebSocket;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

/// <summary>
/// ASP.NET Core middleware that upgrades HTTP requests to WebSocket connections
/// on a configurable path and feeds them into the IRC connection handler.
/// </summary>
public sealed class WebSocketMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IrcConnectionHandler _handler;
    private readonly ILogger<WebSocketMiddleware> _logger;
    private readonly string _path;

    public WebSocketMiddleware(
        RequestDelegate next,
        IrcConnectionHandler handler,
        ILogger<WebSocketMiddleware> logger,
        string path)
    {
        _next = next;
        _handler = handler;
        _logger = logger;
        _path = path;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (context.Request.Path.StartsWithSegments(_path) && context.WebSockets.IsWebSocketRequest)
        {
            // Check for IRC subprotocol per IRCv3 WebSocket spec
            var requestedProtocols = context.WebSockets.WebSocketRequestedProtocols;
            string? subprotocol = null;
            if (requestedProtocols.Contains("irc"))
                subprotocol = "irc";

            var ws = await context.WebSockets.AcceptWebSocketAsync(subprotocol);

            EndPoint? remoteEndPoint = null;
            var remoteIp = context.Connection.RemoteIpAddress;
            if (remoteIp != null)
                remoteEndPoint = new IPEndPoint(remoteIp, context.Connection.RemotePort);

            var connection = new WebSocketConnection(
                ws, _handler, remoteEndPoint,
                _logger);

            _logger.LogInformation("WebSocket client connected from {Remote}", remoteEndPoint);

            await connection.RunAsync(context.RequestAborted);
        }
        else
        {
            await _next(context);
        }
    }
}
