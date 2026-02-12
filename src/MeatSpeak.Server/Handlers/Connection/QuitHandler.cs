namespace MeatSpeak.Server.Handlers.Connection;

using MeatSpeak.Protocol;
using MeatSpeak.Server.Core.Commands;
using MeatSpeak.Server.Core.Sessions;
using MeatSpeak.Server.Data;

[FloodPenalty(0)]
public sealed class QuitHandler : ICommandHandler
{
    private readonly DbWriteQueue? _writeQueue;
    public string Command => IrcConstants.QUIT;
    public SessionState MinimumState => SessionState.Connecting;

    public QuitHandler(DbWriteQueue? writeQueue = null) => _writeQueue = writeQueue;

    public async ValueTask HandleAsync(ISession session, IrcMessage message, CancellationToken ct = default)
    {
        var reason = message.GetParam(0) ?? "Client quit";
        var nickname = session.Info.Nickname;

        // Update user history before disconnecting
        if (_writeQueue != null && nickname != null && session.State >= SessionState.Registered)
        {
            _writeQueue.TryWrite(new UpdateUserDisconnect(nickname, DateTimeOffset.UtcNow, reason));
        }

        await session.DisconnectAsync(reason);
    }
}
