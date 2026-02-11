namespace MeatSpeak.Server.Handlers.Voice;

using MeatSpeak.Protocol;
using MeatSpeak.Server.Core.Commands;
using MeatSpeak.Server.Core.Sessions;

public sealed class VoiceHandler : ICommandHandler
{
    public string Command => IrcConstants.VOICE;
    public SessionState MinimumState => SessionState.Registered;

    public ValueTask HandleAsync(ISession session, IrcMessage message, CancellationToken ct = default)
    {
        // TODO: Dispatch to sub-handlers based on first parameter (JOIN/LEAVE/MUTE/UNMUTE/etc.)
        return ValueTask.CompletedTask;
    }
}
