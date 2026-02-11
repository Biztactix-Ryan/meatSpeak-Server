namespace MeatSpeak.Server.Registration;

using MeatSpeak.Server.Core.Sessions;
using MeatSpeak.Server.Core.Server;
using MeatSpeak.Server.Core.Events;
using MeatSpeak.Server.Numerics;
using Microsoft.Extensions.Logging;

public sealed class RegistrationPipeline
{
    private readonly IServer _server;
    private readonly NumericSender _numerics;
    private readonly ILogger<RegistrationPipeline> _logger;

    public RegistrationPipeline(IServer server, NumericSender numerics, ILogger<RegistrationPipeline> logger)
    {
        _server = server;
        _numerics = numerics;
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
    }
}
