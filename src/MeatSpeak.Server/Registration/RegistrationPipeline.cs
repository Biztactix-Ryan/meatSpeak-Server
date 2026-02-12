namespace MeatSpeak.Server.Registration;

using MeatSpeak.Server.Core.Sessions;
using MeatSpeak.Server.Core.Server;
using MeatSpeak.Server.Core.Events;
using MeatSpeak.Server.Data;
using MeatSpeak.Server.Data.Entities;
using MeatSpeak.Server.Data.Repositories;
using MeatSpeak.Server.Diagnostics;
using MeatSpeak.Server.Handlers.Connection;
using MeatSpeak.Server.Numerics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

public sealed class RegistrationPipeline
{
    private readonly IServer _server;
    private readonly NumericSender _numerics;
    private readonly DbWriteQueue? _writeQueue;
    private readonly IServiceScopeFactory? _scopeFactory;
    private readonly ILogger<RegistrationPipeline> _logger;
    private readonly ServerMetrics _metrics;

    public RegistrationPipeline(IServer server, NumericSender numerics, DbWriteQueue? writeQueue, IServiceScopeFactory? scopeFactory, ILogger<RegistrationPipeline> logger, ServerMetrics metrics)
    {
        _server = server;
        _numerics = numerics;
        _writeQueue = writeQueue;
        _scopeFactory = scopeFactory;
        _logger = logger;
        _metrics = metrics;
    }

    public async ValueTask TryCompleteRegistrationAsync(ISession session)
    {
        if (session.State >= SessionState.Registered)
            return;

        // Need both NICK and USER, and CAP negotiation must be complete (or never started)
        if (session.Info.Nickname == null || session.Info.Username == null)
            return;

        if (session.Info.CapState.InNegotiation && !session.Info.CapState.NegotiationComplete)
            return;

        // Validate server password if configured
        var requiredPass = _server.Config.ServerPassword;
        if (!string.IsNullOrEmpty(requiredPass) && session.Info.ServerPassword != requiredPass)
        {
            await session.SendNumericAsync(_server.Config.ServerName, Protocol.Numerics.ERR_PASSWDMISMATCH,
                "Password incorrect");
            await session.DisconnectAsync("Bad password");
            return;
        }

        // Check server-wide bans (K-lines)
        if (_scopeFactory != null)
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var banRepo = scope.ServiceProvider.GetRequiredService<IBanRepository>();
            var bans = await banRepo.GetAllActiveAsync();
            var prefix = session.Info.Prefix;

            foreach (var ban in bans)
            {
                if (IrcWildcard.Match(ban.Mask, prefix))
                {
                    var reason = string.IsNullOrEmpty(ban.Reason) ? "You are banned from this server" : ban.Reason;
                    _logger.LogInformation("Session {Id} ({Prefix}) rejected: matches K-line {Mask}",
                        session.Id, prefix, ban.Mask);
                    await session.SendNumericAsync(_server.Config.ServerName, Protocol.Numerics.ERR_YOUREBANNEDCREEP,
                        reason);
                    await session.DisconnectAsync($"K-lined: {reason}");
                    return;
                }
            }
        }

        session.State = SessionState.Registered;
        _logger.LogInformation("Session {Id} registered as {Nick}", session.Id, session.Info.Nickname);

        await _numerics.SendWelcomeAsync(session);
        await _numerics.SendIsupportAsync(session);
        await _numerics.SendLusersAsync(session);
        await _numerics.SendMotdAsync(session);

        _metrics.RegistrationCompleted();
        _metrics.RecordRegistrationDuration(ServerMetrics.GetElapsedMs(session.Info.ConnectTimestamp));

        _server.Events.Publish(new SessionRegisteredEvent(session.Id, session.Info.Nickname));

        // MONITOR: notify watchers that this nick is now online
        await MonitorHandler.NotifyOnline(_server, session);

        // Log user history
        _writeQueue?.TryWrite(new AddUserHistory(new UserHistoryEntity
        {
            Nickname = session.Info.Nickname,
            Username = session.Info.Username,
            Hostname = session.Info.Hostname,
            Account = session.Info.Account,
            ConnectedAt = session.Info.ConnectedAt,
        }));
    }
}
