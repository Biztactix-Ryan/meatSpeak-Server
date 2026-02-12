namespace MeatSpeak.Server.Registration;

using MeatSpeak.Server.Core.Sessions;
using MeatSpeak.Server.Core.Server;
using MeatSpeak.Server.Core.Events;
using MeatSpeak.Server.Data;
using MeatSpeak.Server.Data.Entities;
using MeatSpeak.Server.Diagnostics;
using MeatSpeak.Server.Handlers.Connection;
using MeatSpeak.Server.Numerics;
using Microsoft.Extensions.Logging;

public sealed class RegistrationPipeline
{
    private readonly IServer _server;
    private readonly NumericSender _numerics;
    private readonly DbWriteQueue? _writeQueue;
    private readonly ILogger<RegistrationPipeline> _logger;
    private readonly ServerMetrics _metrics;

    public RegistrationPipeline(IServer server, NumericSender numerics, DbWriteQueue? writeQueue, ILogger<RegistrationPipeline> logger, ServerMetrics metrics)
    {
        _server = server;
        _numerics = numerics;
        _writeQueue = writeQueue;
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
