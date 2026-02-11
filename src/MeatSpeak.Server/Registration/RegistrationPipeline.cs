namespace MeatSpeak.Server.Registration;

using MeatSpeak.Server.Core.Sessions;
using MeatSpeak.Server.Core.Server;
using MeatSpeak.Server.Core.Events;
using MeatSpeak.Server.Data.Entities;
using MeatSpeak.Server.Data.Repositories;
using MeatSpeak.Server.Numerics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

public sealed class RegistrationPipeline
{
    private readonly IServer _server;
    private readonly NumericSender _numerics;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<RegistrationPipeline> _logger;

    public RegistrationPipeline(IServer server, NumericSender numerics, IServiceScopeFactory scopeFactory, ILogger<RegistrationPipeline> logger)
    {
        _server = server;
        _numerics = numerics;
        _scopeFactory = scopeFactory;
        _logger = logger;
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

        _server.Events.Publish(new SessionRegisteredEvent(session.Id, session.Info.Nickname));

        // Log user history
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var userHistory = scope.ServiceProvider.GetRequiredService<IUserHistoryRepository>();
            await userHistory.AddAsync(new UserHistoryEntity
            {
                Nickname = session.Info.Nickname,
                Username = session.Info.Username,
                Hostname = session.Info.Hostname,
                Account = session.Info.Account,
                ConnectedAt = session.Info.ConnectedAt,
            });
        }
        catch { /* DB logging failure should not break registration */ }
    }
}
