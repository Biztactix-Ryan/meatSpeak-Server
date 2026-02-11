using System.Text.Json;
using MeatSpeak.Server.Diagnostics;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace MeatSpeak.Server.Tests.Diagnostics;

public class BenchmarkServiceTests
{
    private readonly ServerMetrics _metrics = new();
    private readonly IHostApplicationLifetime _lifetime = Substitute.For<IHostApplicationLifetime>();
    private readonly ILogger<BenchmarkService> _logger = Substitute.For<ILogger<BenchmarkService>>();

    [Fact]
    public void ServerMetrics_ConnectionsAccepted_ReflectsAcceptedCount()
    {
        Assert.Equal(0, _metrics.ConnectionsAccepted);
        _metrics.ConnectionAccepted();
        Assert.Equal(1, _metrics.ConnectionsAccepted);
        _metrics.ConnectionAccepted();
        Assert.Equal(2, _metrics.ConnectionsAccepted);
    }

    [Fact]
    public void ServerMetrics_ConnectionsActive_ReflectsActiveCount()
    {
        Assert.Equal(0, _metrics.ConnectionsActive);
        _metrics.ConnectionActive();
        Assert.Equal(1, _metrics.ConnectionsActive);
        _metrics.ConnectionActive();
        Assert.Equal(2, _metrics.ConnectionsActive);
        _metrics.ConnectionClosed();
        Assert.Equal(1, _metrics.ConnectionsActive);
        _metrics.ConnectionClosed();
        Assert.Equal(0, _metrics.ConnectionsActive);
    }

    [Fact]
    public async Task CallsStopApplication_WhenAllClientsDisconnect()
    {
        var svc = new BenchmarkService(_metrics, _lifetime, _logger, outputPath: null);

        // Simulate a client already connected before start
        _metrics.ConnectionAccepted();
        _metrics.ConnectionActive();

        await svc.StartAsync(CancellationToken.None);

        // Disconnect the client — service should detect this within a poll cycle
        _metrics.ConnectionClosed();

        // Wait for poll (500ms) + grace period (2s) + buffer
        await Task.Delay(4000);

        _lifetime.Received().StopApplication();

        await svc.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task WritesJsonFile_WhenOutputPathSet()
    {
        var outputPath = Path.Combine(Path.GetTempPath(), $"benchmark-test-{Guid.NewGuid()}.json");
        try
        {
            var svc = new BenchmarkService(_metrics, _lifetime, _logger, outputPath);

            // Simulate some activity
            _metrics.ConnectionAccepted();
            _metrics.ConnectionActive();
            _metrics.CommandDispatched();
            _metrics.MessageBroadcast();

            await svc.StartAsync(CancellationToken.None);

            _metrics.ConnectionClosed();

            // Wait for poll + grace + buffer
            await Task.Delay(4000);

            _lifetime.Received().StopApplication();
            Assert.True(File.Exists(outputPath), "Expected server metrics JSON file to be written");

            var json = await File.ReadAllTextAsync(outputPath);
            var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            Assert.Equal(1, root.GetProperty("counters").GetProperty("connections_accepted").GetInt64());
            Assert.Equal(0, root.GetProperty("counters").GetProperty("connections_active").GetInt64());
            Assert.Equal(1, root.GetProperty("counters").GetProperty("commands_dispatched").GetInt64());
            Assert.Equal(1, root.GetProperty("counters").GetProperty("messages_broadcast").GetInt64());
            Assert.True(root.TryGetProperty("histograms", out _), "Expected histograms section in output");

            await svc.StopAsync(CancellationToken.None);
        }
        finally
        {
            if (File.Exists(outputPath))
                File.Delete(outputPath);
        }
    }

    [Fact]
    public async Task DoesNotWriteFile_WhenOutputPathNull()
    {
        var svc = new BenchmarkService(_metrics, _lifetime, _logger, outputPath: null);

        _metrics.ConnectionAccepted();
        _metrics.ConnectionActive();

        await svc.StartAsync(CancellationToken.None);
        _metrics.ConnectionClosed();

        await Task.Delay(4000);

        _lifetime.Received().StopApplication();
        // No exception, no file — just verifying clean execution with null path

        await svc.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task StopAsync_CancelsPollBeforeAnyConnection()
    {
        var svc = new BenchmarkService(_metrics, _lifetime, _logger, outputPath: null);

        await svc.StartAsync(CancellationToken.None);

        // No connections ever — stop immediately
        await svc.StopAsync(CancellationToken.None);

        // StopApplication should NOT have been called since no clients ever connected
        _lifetime.DidNotReceive().StopApplication();
    }
}
