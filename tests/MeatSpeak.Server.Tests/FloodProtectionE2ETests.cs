using System.Net;
using System.Net.Sockets;
using System.Text;
using MeatSpeak.Server.Core.Capabilities;
using MeatSpeak.Server.Core.Commands;
using MeatSpeak.Server.Core.Events;
using MeatSpeak.Server.Core.Modes;
using MeatSpeak.Server.Core.Server;
using MeatSpeak.Server.Diagnostics;
using MeatSpeak.Server.Events;
using MeatSpeak.Server.Handlers.Channels;
using MeatSpeak.Server.Handlers.Connection;
using MeatSpeak.Server.Handlers.Messaging;
using MeatSpeak.Server.Numerics;
using MeatSpeak.Server.Registration;
using MeatSpeak.Server.State;
using MeatSpeak.Server.Transport.Tcp;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace MeatSpeak.Server.Tests;

public class FloodProtectionE2ETests : IDisposable
{
    private readonly ServerConfig _config;
    private readonly ServerState _serverState;
    private readonly ServerMetrics _metrics;
    private readonly TcpServer _tcpServer;
    private readonly int _port;

    public FloodProtectionE2ETests()
    {
        _config = new ServerConfig
        {
            ServerName = "flood-test.local",
            Flood = new FloodConfig
            {
                Enabled = true,
                BurstLimit = 5,
                TokenIntervalSeconds = 2.0,
                ExcessFloodThreshold = 10,
            },
        };

        _metrics = new ServerMetrics();
        var commands = new CommandRegistry(NullLogger<CommandRegistry>.Instance);
        var modes = new ModeRegistry();
        modes.RegisterStandardModes();
        var caps = new CapabilityRegistry();
        IEventBus events = new InMemoryEventBus();

        _serverState = new ServerState(_config, commands, modes, caps, events, _metrics);

        var numerics = new NumericSender(_serverState);
        var registration = new RegistrationPipeline(
            _serverState, numerics, null,
            NullLogger<RegistrationPipeline>.Instance, _metrics);

        // Register handlers
        commands.Register(new PingHandler());
        commands.Register(new PongHandler());
        commands.Register(new PassHandler(_serverState));
        commands.Register(new NickHandler(_serverState, registration));
        commands.Register(new UserHandler(_serverState, registration));
        commands.Register(new QuitHandler());
        commands.Register(new CapHandler(_serverState, registration));
        commands.Register(new JoinHandler(_serverState));
        commands.Register(new PrivmsgHandler(_serverState, metrics: _metrics));
        commands.Register(new NoticeHandler(_serverState, metrics: _metrics));
        commands.Register(new PartHandler(_serverState));
        commands.Register(new ModeHandler(_serverState));
        commands.Register(new TopicHandler(_serverState));
        commands.Register(new NamesHandler(_serverState));
        commands.Register(new ListHandler(_serverState));
        commands.Register(new WhoHandler(_serverState));

        var handler = new IrcConnectionHandler(
            _serverState,
            NullLogger<IrcConnectionHandler>.Instance,
            _metrics);

        // Start TCP server on a random available port
        _tcpServer = new TcpServer(handler, NullLogger<TcpServer>.Instance);
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        _port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();

        _tcpServer.Start(new IPEndPoint(IPAddress.Loopback, _port));
    }

    public void Dispose()
    {
        _tcpServer.Dispose();
    }

    private async Task<TcpClient> ConnectAndRegisterAsync(string nick, CancellationToken ct = default)
    {
        var client = new TcpClient();
        await client.ConnectAsync(IPAddress.Loopback, _port, ct);
        var stream = client.GetStream();
        var writer = new StreamWriter(stream, new UTF8Encoding(false)) { NewLine = "\r\n", AutoFlush = true };
        var reader = new StreamReader(stream, new UTF8Encoding(false));

        await writer.WriteLineAsync($"NICK {nick}");
        await writer.WriteLineAsync($"USER {nick} 0 * :Flood Test User");

        // Read until we get 001 (RPL_WELCOME) or timeout
        using var regCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        regCts.CancelAfter(TimeSpan.FromSeconds(5));

        while (!regCts.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync(regCts.Token);
            if (line == null) break;
            // 001 = RPL_WELCOME
            if (line.Contains(" 001 ")) break;
        }

        return client;
    }

    private static StreamWriter GetWriter(TcpClient client)
        => new(client.GetStream(), new UTF8Encoding(false)) { NewLine = "\r\n", AutoFlush = true };

    private static StreamReader GetReader(TcpClient client)
        => new(client.GetStream(), new UTF8Encoding(false));

    // ─── E2E: Normal usage within burst is fine ───

    [Fact]
    public async Task NormalUsage_CommandsProcessedSuccessfully()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        using var client = await ConnectAndRegisterAsync("normal1", cts.Token);
        var writer = GetWriter(client);
        var reader = GetReader(client);

        // Join a channel (cost=2, burst=5, so uses 2 tokens -> 3 left)
        await writer.WriteLineAsync("JOIN #test");

        // Read until we get JOIN echo or channel response
        var gotJoin = false;
        using var readCts = CancellationTokenSource.CreateLinkedTokenSource(cts.Token);
        readCts.CancelAfter(TimeSpan.FromSeconds(3));
        try
        {
            while (!readCts.IsCancellationRequested)
            {
                var line = await reader.ReadLineAsync(readCts.Token);
                if (line == null) break;
                if (line.Contains("JOIN") || line.Contains("#test"))
                {
                    gotJoin = true;
                    break;
                }
            }
        }
        catch (OperationCanceledException) { }

        Assert.True(gotJoin, "Expected JOIN response from server");
    }

    // ─── E2E: Rapid fire causes excess flood disconnect ───

    [Fact]
    public async Task RapidFireFlood_DisconnectsClient()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        using var client = await ConnectAndRegisterAsync("flooder1", cts.Token);
        var writer = GetWriter(client);

        // Registration consumed some tokens (NICK cost=2, USER cost=1 → 2 left from burst=5).
        // Now blast PRIVMSG at cost=2 each with zero delay.
        // This should exhaust tokens quickly and trigger excess flood.
        var disconnected = false;
        try
        {
            for (int i = 0; i < 50; i++)
                await writer.WriteLineAsync($"PRIVMSG #nonexistent :flood message {i}");

            // Try to read — if we're disconnected, read will return null or throw
            var reader = GetReader(client);
            using var readCts = CancellationTokenSource.CreateLinkedTokenSource(cts.Token);
            readCts.CancelAfter(TimeSpan.FromSeconds(2));

            try
            {
                while (!readCts.IsCancellationRequested)
                {
                    var line = await reader.ReadLineAsync(readCts.Token);
                    if (line == null)
                    {
                        disconnected = true;
                        break;
                    }
                    // Look for ERROR message indicating excess flood
                    if (line.Contains("ERROR") && line.Contains("Excess Flood"))
                    {
                        disconnected = true;
                        break;
                    }
                }
            }
            catch (IOException) { disconnected = true; }
            catch (OperationCanceledException) { }
        }
        catch (IOException)
        {
            // Connection reset — server kicked us
            disconnected = true;
        }

        Assert.True(disconnected, "Expected server to disconnect the flooding client");
        Assert.True(_metrics.ExcessFloodDisconnects > 0, "Expected excess flood metric to be incremented");
    }

    // ─── E2E: Throttled commands are silently dropped ───

    [Fact]
    public async Task ThrottledCommands_SilentlyDropped()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        using var client = await ConnectAndRegisterAsync("throttle1", cts.Token);
        var writer = GetWriter(client);
        var reader = GetReader(client);

        // After registration (NICK=2, USER=1), 2 tokens left.
        // Send a few LIST commands (cost=1) — first 2 allowed, rest throttled but not disconnected
        // (debt will stay below threshold=10)
        for (int i = 0; i < 5; i++)
            await writer.WriteLineAsync("LIST");

        // Give the server time to process
        await Task.Delay(200, cts.Token);

        Assert.True(_metrics.CommandsThrottled > 0, "Expected some commands to be throttled");

        // Client should still be connected (not excess flood)
        Assert.True(client.Connected);
    }

    // ─── E2E: Zero-cost PING/PONG never throttled ───

    [Fact]
    public async Task PingPong_NeverThrottled()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        using var client = await ConnectAndRegisterAsync("pinguser1", cts.Token);
        var writer = GetWriter(client);
        var reader = GetReader(client);

        var throttledBefore = _metrics.CommandsThrottled;

        // Blast PINGs — these have cost=0, so should all succeed
        for (int i = 0; i < 30; i++)
            await writer.WriteLineAsync($"PING flood{i}");

        // Read PONGs
        var pongCount = 0;
        using var readCts = CancellationTokenSource.CreateLinkedTokenSource(cts.Token);
        readCts.CancelAfter(TimeSpan.FromSeconds(3));
        try
        {
            while (!readCts.IsCancellationRequested)
            {
                var line = await reader.ReadLineAsync(readCts.Token);
                if (line == null) break;
                if (line.Contains("PONG"))
                    pongCount++;
                if (pongCount >= 30) break;
            }
        }
        catch (OperationCanceledException) { }

        Assert.Equal(30, pongCount);
        Assert.Equal(throttledBefore, _metrics.CommandsThrottled); // No new throttles from PINGs
    }

    // ─── E2E: Flood disabled allows everything ───

    [Fact]
    public async Task FloodDisabled_NoThrottling()
    {
        // Reconfigure with flood disabled
        _config.Flood.Enabled = false;

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        using var client = await ConnectAndRegisterAsync("nolimit1", cts.Token);
        var writer = GetWriter(client);

        var throttledBefore = _metrics.CommandsThrottled;
        var floodBefore = _metrics.ExcessFloodDisconnects;

        // Blast many commands without delay
        for (int i = 0; i < 50; i++)
            await writer.WriteLineAsync("LIST");

        await Task.Delay(200, cts.Token);

        // No throttling and no disconnect
        Assert.Equal(throttledBefore, _metrics.CommandsThrottled);
        Assert.Equal(floodBefore, _metrics.ExcessFloodDisconnects);
        Assert.True(client.Connected);

        // Restore config for other tests
        _config.Flood.Enabled = true;
    }

    // ─── E2E: Second client unaffected by first client's flood ───

    [Fact]
    public async Task FloodIsolation_OtherClientsUnaffected()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        // Connect two clients
        using var goodClient = await ConnectAndRegisterAsync("gooduser", cts.Token);
        using var badClient = await ConnectAndRegisterAsync("baduser", cts.Token);

        var badWriter = GetWriter(badClient);

        // Flood the bad client until disconnect
        try
        {
            for (int i = 0; i < 50; i++)
                await badWriter.WriteLineAsync($"PRIVMSG #x :spam {i}");
        }
        catch (IOException) { /* expected — connection killed */ }

        await Task.Delay(300, cts.Token);

        // Good client should still work fine
        var goodWriter = GetWriter(goodClient);
        var goodReader = GetReader(goodClient);

        await goodWriter.WriteLineAsync("PING alive");

        var gotPong = false;
        using var readCts = CancellationTokenSource.CreateLinkedTokenSource(cts.Token);
        readCts.CancelAfter(TimeSpan.FromSeconds(3));
        try
        {
            while (!readCts.IsCancellationRequested)
            {
                var line = await goodReader.ReadLineAsync(readCts.Token);
                if (line == null) break;
                if (line.Contains("PONG"))
                {
                    gotPong = true;
                    break;
                }
            }
        }
        catch (OperationCanceledException) { }

        Assert.True(gotPong, "Good client should still receive PONG after bad client was disconnected");
    }

    // ─── E2E: Steady slow rate stays under limit ───

    [Fact]
    public async Task SteadySlowRate_NeverThrottled()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        using var client = await ConnectAndRegisterAsync("slow1", cts.Token);
        var writer = GetWriter(client);
        var reader = GetReader(client);

        var throttledBefore = _metrics.CommandsThrottled;

        // Send PINGs at a rate slower than token regeneration (2s interval)
        // This ensures tokens regenerate between commands
        for (int i = 0; i < 5; i++)
        {
            await writer.WriteLineAsync($"PING slow{i}");
            await Task.Delay(2100, cts.Token); // Slightly more than token interval
        }

        // Read all PONGs
        var pongCount = 0;
        using var readCts = CancellationTokenSource.CreateLinkedTokenSource(cts.Token);
        readCts.CancelAfter(TimeSpan.FromSeconds(3));
        try
        {
            while (!readCts.IsCancellationRequested)
            {
                var line = await reader.ReadLineAsync(readCts.Token);
                if (line == null) break;
                if (line.Contains("PONG")) pongCount++;
                if (pongCount >= 5) break;
            }
        }
        catch (OperationCanceledException) { }

        Assert.Equal(5, pongCount);
        Assert.Equal(throttledBefore, _metrics.CommandsThrottled);
    }

    // ─── E2E: Server metrics reflect flood activity ───

    [Fact]
    public async Task ServerMetrics_ReflectFloodActivity()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        var throttledBefore = _metrics.CommandsThrottled;
        var floodBefore = _metrics.ExcessFloodDisconnects;
        var dispatchedBefore = _metrics.GetSnapshot().CommandsDispatched;

        using var client = await ConnectAndRegisterAsync("metrics1", cts.Token);
        var writer = GetWriter(client);

        // Send commands to cause some throttling
        for (int i = 0; i < 8; i++)
            await writer.WriteLineAsync("LIST");

        await Task.Delay(300, cts.Token);

        var snapshot = _metrics.GetSnapshot();
        Assert.True(snapshot.CommandsDispatched > dispatchedBefore,
            "Expected dispatched commands to increase");
        Assert.True(_metrics.CommandsThrottled > throttledBefore,
            "Expected throttled commands to increase");
    }
}
