namespace MeatSpeak.Server;

using MeatSpeak.Protocol;
using MeatSpeak.Server.Core.Server;
using MeatSpeak.Server.Core.Sessions;
using MeatSpeak.Server.Diagnostics;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

public sealed class PingTimeoutService : BackgroundService
{
    private readonly IServer _server;
    private readonly ILogger<PingTimeoutService> _logger;
    private readonly ServerMetrics _metrics;
    private readonly TimeSpan _checkInterval;
    private readonly TimeSpan _pingInterval;
    private readonly TimeSpan _pingTimeout;

    public PingTimeoutService(IServer server, ILogger<PingTimeoutService> logger, ServerMetrics metrics)
    {
        _server = server;
        _logger = logger;
        _metrics = metrics;
        _pingInterval = TimeSpan.FromSeconds(server.Config.PingInterval);
        _pingTimeout = TimeSpan.FromSeconds(server.Config.PingTimeout);
        // Check at half the ping interval for responsive detection
        _checkInterval = TimeSpan.FromSeconds(Math.Max(server.Config.PingInterval / 2, 10));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Ping timeout service started (interval={Interval}s, timeout={Timeout}s)",
            _server.Config.PingInterval, _server.Config.PingTimeout);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(_checkInterval, stoppingToken);
                await CheckSessionsAsync();
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in ping timeout check");
            }
        }
    }

    internal async Task CheckSessionsAsync()
    {
        var now = DateTimeOffset.UtcNow;

        foreach (var (_, session) in _server.Sessions)
        {
            if (session.State == SessionState.Disconnecting)
                continue;

            var idle = now - session.Info.LastActivity;

            if (idle > _pingTimeout)
            {
                _logger.LogInformation("Ping timeout for {Nick} ({Id}), idle {Idle}s",
                    session.Info.Nickname ?? "?", session.Id, (int)idle.TotalSeconds);
                _metrics.PingTimeout();

                try
                {
                    await session.DisconnectAsync("Ping timeout");
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Error disconnecting timed-out session {Id}", session.Id);
                }
            }
            else if (idle > _pingInterval && session.State >= SessionState.Registered)
            {
                try
                {
                    await session.SendMessageAsync(null, IrcConstants.PING, _server.Config.ServerName);
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Error sending PING to {Id}", session.Id);
                }
            }
        }
    }
}
