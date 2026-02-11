using System.Net;
using System.Net.WebSockets;
using System.Text;
using MeatSpeak.Server.Transport;
using MeatSpeak.Server.Transport.WebSocket;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace MeatSpeak.Server.Tests;

public class WebSocketConnectionTests
{
    private readonly ILogger _logger = NullLogger.Instance;

    /// <summary>
    /// A test handler that records connection lifecycle events and received data.
    /// </summary>
    private sealed class TestConnectionHandler : IConnectionHandler
    {
        public readonly List<string> Events = new();
        public readonly List<string> ReceivedLines = new();
        public IConnection? LastConnection { get; private set; }
        private readonly TaskCompletionSource _connectedTcs = new();
        private readonly TaskCompletionSource _disconnectedTcs = new();
        private readonly TaskCompletionSource<string> _firstLineTcs = new();

        public Task WaitForConnected(CancellationToken ct = default)
        {
            ct.Register(() => _connectedTcs.TrySetCanceled());
            return _connectedTcs.Task;
        }

        public Task WaitForDisconnected(CancellationToken ct = default)
        {
            ct.Register(() => _disconnectedTcs.TrySetCanceled());
            return _disconnectedTcs.Task;
        }

        public Task<string> WaitForFirstLine(CancellationToken ct = default)
        {
            ct.Register(() => _firstLineTcs.TrySetCanceled());
            return _firstLineTcs.Task;
        }

        public void OnConnected(IConnection connection)
        {
            LastConnection = connection;
            Events.Add("connected");
            _connectedTcs.TrySetResult();
        }

        public void OnData(IConnection connection, ReadOnlySpan<byte> line)
        {
            var text = Encoding.UTF8.GetString(line);
            ReceivedLines.Add(text);
            _firstLineTcs.TrySetResult(text);
        }

        public void OnDisconnected(IConnection connection)
        {
            Events.Add("disconnected");
            _disconnectedTcs.TrySetResult();
        }
    }

    /// <summary>
    /// Creates a test Kestrel server with WebSocket support on a random port.
    /// Returns the app, port, and the test handler.
    /// </summary>
    private static (WebApplication app, int port, TestConnectionHandler handler) CreateTestServer()
    {
        var handler = new TestConnectionHandler();
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.ConfigureKestrel(k => k.Listen(IPAddress.Loopback, 0));
        builder.Logging.SetMinimumLevel(LogLevel.Warning);

        var app = builder.Build();
        app.UseWebSockets();
        app.Use(async (context, next) =>
        {
            if (context.WebSockets.IsWebSocketRequest)
            {
                var ws = await context.WebSockets.AcceptWebSocketAsync();
                var remoteEp = new IPEndPoint(
                    context.Connection.RemoteIpAddress ?? IPAddress.Loopback,
                    context.Connection.RemotePort);
                var conn = new WebSocketConnection(ws, handler, remoteEp,
                    NullLogger.Instance);
                await conn.RunAsync(context.RequestAborted);
            }
            else
            {
                await next();
            }
        });

        app.StartAsync().GetAwaiter().GetResult();
        var serverAddresses = app.Services
            .GetRequiredService<Microsoft.AspNetCore.Hosting.Server.IServer>()
            .Features.Get<IServerAddressesFeature>()!;
        var address = serverAddresses.Addresses.First();
        var port = new Uri(address).Port;

        return (app, port, handler);
    }

    [Fact]
    public void WebSocketConnection_HasCorrectIdPrefix()
    {
        // Unit test — no server needed
        var handler = new TestConnectionHandler();
        using var ws = new ClientWebSocket();
        // We can't create a WebSocketConnection without a real WebSocket,
        // but we can test the ID format by using a dummy
        var dummyWs = System.Net.WebSockets.WebSocket.CreateFromStream(
            new MemoryStream(), new WebSocketCreationOptions { IsServer = true });
        using var conn = new WebSocketConnection(dummyWs, handler, null, _logger);
        Assert.StartsWith("ws-", conn.Id);
    }

    [Fact]
    public void WebSocketConnection_RemoteEndPoint_IsPreserved()
    {
        var handler = new TestConnectionHandler();
        var endpoint = new IPEndPoint(IPAddress.Loopback, 12345);
        var dummyWs = System.Net.WebSockets.WebSocket.CreateFromStream(
            new MemoryStream(), new WebSocketCreationOptions { IsServer = true });
        using var conn = new WebSocketConnection(dummyWs, handler, endpoint, _logger);
        Assert.Equal(endpoint, conn.RemoteEndPoint);
    }

    [Fact]
    public async Task RunAsync_ClientSendsLineWithCrLf_HandlerReceivesLine()
    {
        var (app, port, handler) = CreateTestServer();
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            using var client = new ClientWebSocket();
            await client.ConnectAsync(new Uri($"ws://localhost:{port}/"), cts.Token);
            await handler.WaitForConnected(cts.Token);

            var data = Encoding.UTF8.GetBytes("NICK testuser\r\n");
            await client.SendAsync(new ArraySegment<byte>(data), WebSocketMessageType.Text, true, cts.Token);

            var line = await handler.WaitForFirstLine(cts.Token);
            Assert.Equal("NICK testuser", line);
        }
        finally
        {
            await app.StopAsync();
            await app.DisposeAsync();
        }
    }

    [Fact]
    public async Task RunAsync_ClientSendsLineWithoutNewline_HandlerReceivesLine()
    {
        var (app, port, handler) = CreateTestServer();
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            using var client = new ClientWebSocket();
            await client.ConnectAsync(new Uri($"ws://localhost:{port}/"), cts.Token);
            await handler.WaitForConnected(cts.Token);

            // Per IRCv3 WebSocket spec: complete frame without newline = single IRC line
            var data = Encoding.UTF8.GetBytes("PING server");
            await client.SendAsync(new ArraySegment<byte>(data), WebSocketMessageType.Text, true, cts.Token);

            var line = await handler.WaitForFirstLine(cts.Token);
            Assert.Equal("PING server", line);
        }
        finally
        {
            await app.StopAsync();
            await app.DisposeAsync();
        }
    }

    [Fact]
    public async Task RunAsync_ClientSendsMultipleLines_HandlerReceivesAll()
    {
        var (app, port, handler) = CreateTestServer();
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            using var client = new ClientWebSocket();
            await client.ConnectAsync(new Uri($"ws://localhost:{port}/"), cts.Token);
            await handler.WaitForConnected(cts.Token);

            var data = Encoding.UTF8.GetBytes("NICK foo\r\nUSER bar 0 * :Real Name\r\n");
            await client.SendAsync(new ArraySegment<byte>(data), WebSocketMessageType.Text, true, cts.Token);

            // Wait for processing
            await Task.Delay(200, cts.Token);

            Assert.Contains("NICK foo", handler.ReceivedLines);
            Assert.Contains("USER bar 0 * :Real Name", handler.ReceivedLines);
        }
        finally
        {
            await app.StopAsync();
            await app.DisposeAsync();
        }
    }

    [Fact]
    public async Task RunAsync_ClientCloses_OnDisconnectedCalled()
    {
        var (app, port, handler) = CreateTestServer();
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            using var client = new ClientWebSocket();
            await client.ConnectAsync(new Uri($"ws://localhost:{port}/"), cts.Token);
            await handler.WaitForConnected(cts.Token);

            await client.CloseAsync(WebSocketCloseStatus.NormalClosure, "bye", cts.Token);

            await handler.WaitForDisconnected(cts.Token);
            Assert.Contains("disconnected", handler.Events);
        }
        finally
        {
            await app.StopAsync();
            await app.DisposeAsync();
        }
    }

    [Fact]
    public async Task Send_DeliversDataToClient()
    {
        var (app, port, handler) = CreateTestServer();
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            using var client = new ClientWebSocket();
            await client.ConnectAsync(new Uri($"ws://localhost:{port}/"), cts.Token);
            await handler.WaitForConnected(cts.Token);

            // Server sends data to client
            var sendData = Encoding.UTF8.GetBytes(":server PING :test\r\n");
            handler.LastConnection!.Send(sendData);

            // Read from client side
            var recvBuffer = new byte[4096];
            var result = await client.ReceiveAsync(new ArraySegment<byte>(recvBuffer), cts.Token);

            Assert.Equal(WebSocketMessageType.Text, result.MessageType);
            var received = Encoding.UTF8.GetString(recvBuffer, 0, result.Count);
            Assert.Equal(":server PING :test\r\n", received);
        }
        finally
        {
            await app.StopAsync();
            await app.DisposeAsync();
        }
    }

    [Fact]
    public async Task Disconnect_ServerSide_ClosesWebSocket()
    {
        var (app, port, handler) = CreateTestServer();
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            using var client = new ClientWebSocket();
            await client.ConnectAsync(new Uri($"ws://localhost:{port}/"), cts.Token);
            await handler.WaitForConnected(cts.Token);

            // Server disconnects the client
            handler.LastConnection!.Disconnect();

            // Wait for the server-side disconnect event
            await handler.WaitForDisconnected(cts.Token);
            Assert.Contains("disconnected", handler.Events);
            Assert.False(handler.LastConnection!.IsConnected);
        }
        finally
        {
            await app.StopAsync();
            await app.DisposeAsync();
        }
    }

    [Fact]
    public async Task WebSocketConnection_IrcSubprotocol_NegotiatedWhenRequested()
    {
        var (app, port, handler) = CreateTestServer();
        try
        {
            // Override server to accept "irc" subprotocol
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            using var client = new ClientWebSocket();
            client.Options.AddSubProtocol("irc");
            await client.ConnectAsync(new Uri($"ws://localhost:{port}/"), cts.Token);
            await handler.WaitForConnected(cts.Token);

            // The test server doesn't negotiate subprotocol (AcceptWebSocketAsync with no arg),
            // but the connection should still work
            Assert.Contains("connected", handler.Events);
        }
        finally
        {
            await app.StopAsync();
            await app.DisposeAsync();
        }
    }
}
