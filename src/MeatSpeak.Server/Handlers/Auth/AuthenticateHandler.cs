namespace MeatSpeak.Server.Handlers.Auth;

using MeatSpeak.Protocol;
using MeatSpeak.Server.Core.Commands;
using MeatSpeak.Server.Core.Sessions;

public sealed class AuthenticateHandler : ICommandHandler
{
    public string Command => IrcConstants.AUTHENTICATE;
    public SessionState MinimumState => SessionState.Registered;

    public ValueTask HandleAsync(ISession session, IrcMessage message, CancellationToken ct = default)
    {
        // TODO: Implement Ed25519 mutual auth via MeatSpeak.Identity
        // MutualAuth.CreateServerHello(), VerifyClientHello()
        return ValueTask.CompletedTask;
    }
}
