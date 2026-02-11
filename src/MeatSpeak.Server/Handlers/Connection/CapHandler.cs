namespace MeatSpeak.Server.Handlers.Connection;

using MeatSpeak.Protocol;
using MeatSpeak.Server.Core.Commands;
using MeatSpeak.Server.Core.Sessions;
using MeatSpeak.Server.Core.Server;
using MeatSpeak.Server.Registration;

public sealed class CapHandler : ICommandHandler
{
    private readonly IServer _server;
    private readonly RegistrationPipeline _registration;
    public string Command => IrcConstants.CAP;
    public SessionState MinimumState => SessionState.Connecting;

    public CapHandler(IServer server, RegistrationPipeline registration)
    {
        _server = server;
        _registration = registration;
    }

    public async ValueTask HandleAsync(ISession session, IrcMessage message, CancellationToken ct = default)
    {
        if (message.Parameters.Count < 1) return;

        var subCommand = message.GetParam(0)!.ToUpperInvariant();
        session.Info.CapState.InNegotiation = true;

        switch (subCommand)
        {
            case "LS":
                await HandleLs(session);
                break;
            case "REQ":
                await HandleReq(session, message);
                break;
            case "END":
                await HandleEnd(session);
                break;
            case "LIST":
                await HandleList(session);
                break;
        }
    }

    private async ValueTask HandleLs(ISession session)
    {
        var capList = _server.Capabilities.GetCapLsString();
        await session.SendMessageAsync(_server.Config.ServerName, IrcConstants.CAP,
            session.Info.Nickname ?? "*", "LS", capList);
    }

    private async ValueTask HandleReq(ISession session, IrcMessage message)
    {
        var requested = message.GetParam(1)?.Split(' ', StringSplitOptions.RemoveEmptyEntries) ?? Array.Empty<string>();
        var acked = new List<string>();
        var naked = new List<string>();

        foreach (var capName in requested)
        {
            var name = capName.TrimStart('-');
            var removing = capName.StartsWith('-');

            if (_server.Capabilities.Resolve(name) != null)
            {
                if (removing)
                {
                    session.Info.CapState.Acknowledged.Remove(name);
                    var cap = _server.Capabilities.Resolve(name);
                    cap?.OnDisabled(session);
                }
                else
                {
                    session.Info.CapState.Acknowledged.Add(name);
                    var cap = _server.Capabilities.Resolve(name);
                    cap?.OnEnabled(session);
                }
                acked.Add(capName);
            }
            else
            {
                naked.Add(capName);
            }
        }

        if (naked.Count > 0)
        {
            await session.SendMessageAsync(_server.Config.ServerName, IrcConstants.CAP,
                session.Info.Nickname ?? "*", "NAK", string.Join(" ", naked));
        }
        if (acked.Count > 0)
        {
            await session.SendMessageAsync(_server.Config.ServerName, IrcConstants.CAP,
                session.Info.Nickname ?? "*", "ACK", string.Join(" ", acked));
        }
    }

    private async ValueTask HandleEnd(ISession session)
    {
        session.Info.CapState.NegotiationComplete = true;
        await _registration.TryCompleteRegistrationAsync(session);
    }

    private async ValueTask HandleList(ISession session)
    {
        var caps = string.Join(" ", session.Info.CapState.Acknowledged);
        await session.SendMessageAsync(_server.Config.ServerName, IrcConstants.CAP,
            session.Info.Nickname ?? "*", "LIST", caps);
    }
}
