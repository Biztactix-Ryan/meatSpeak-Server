namespace MeatSpeak.Server.Handlers.Connection;

using MeatSpeak.Protocol;
using MeatSpeak.Server.Core.Commands;
using MeatSpeak.Server.Core.Sessions;
using MeatSpeak.Server.Data.Repositories;
using Microsoft.Extensions.DependencyInjection;

public sealed class QuitHandler : ICommandHandler
{
    private readonly IServiceScopeFactory? _scopeFactory;
    public string Command => IrcConstants.QUIT;
    public SessionState MinimumState => SessionState.Connecting;

    public QuitHandler(IServiceScopeFactory? scopeFactory = null) => _scopeFactory = scopeFactory;

    public async ValueTask HandleAsync(ISession session, IrcMessage message, CancellationToken ct = default)
    {
        var reason = message.GetParam(0) ?? "Client quit";
        var nickname = session.Info.Nickname;

        // Update user history before disconnecting
        if (_scopeFactory != null && nickname != null && session.State >= SessionState.Registered)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var userHistory = scope.ServiceProvider.GetRequiredService<IUserHistoryRepository>();
                var entries = await userHistory.GetByNicknameAsync(nickname, 1, ct);
                if (entries.Count > 0 && entries[0].DisconnectedAt == null)
                    await userHistory.UpdateDisconnectAsync(entries[0].Id, DateTimeOffset.UtcNow, reason, ct);
            }
            catch { /* DB failure should not prevent disconnect */ }
        }

        await session.DisconnectAsync(reason);
    }
}
