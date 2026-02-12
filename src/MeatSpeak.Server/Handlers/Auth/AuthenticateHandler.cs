namespace MeatSpeak.Server.Handlers.Auth;

using System.Text;
using MeatSpeak.Protocol;
using MeatSpeak.Server.AdminApi.Auth;
using MeatSpeak.Server.Capabilities;
using MeatSpeak.Server.Core.Commands;
using MeatSpeak.Server.Core.Sessions;
using MeatSpeak.Server.Core.Server;
using MeatSpeak.Server.Data.Repositories;
using Microsoft.Extensions.DependencyInjection;

[FloodPenalty(5)]
public sealed class AuthenticateHandler : ICommandHandler
{
    private readonly IServer _server;
    private readonly IServiceScopeFactory _scopeFactory;
    public string Command => IrcConstants.AUTHENTICATE;
    public SessionState MinimumState => SessionState.Connecting;

    public AuthenticateHandler(IServer server, IServiceScopeFactory scopeFactory)
    {
        _server = server;
        _scopeFactory = scopeFactory;
    }

    public async ValueTask HandleAsync(ISession session, IrcMessage message, CancellationToken ct = default)
    {
        if (!CapHelper.HasCap(session, "sasl"))
        {
            await session.SendNumericAsync(_server.Config.ServerName, Numerics.ERR_SASLFAIL,
                "SASL authentication failed (cap not enabled)");
            return;
        }

        if (session.Info.Account != null)
        {
            await session.SendNumericAsync(_server.Config.ServerName, Numerics.ERR_SASLALREADY,
                "You have already authenticated using SASL");
            return;
        }

        var param = message.GetParam(0);
        if (param == null) return;

        if (param.Equals("PLAIN", StringComparison.OrdinalIgnoreCase))
        {
            // Client is starting PLAIN auth - send "+" to request credentials
            await session.SendMessageAsync(null, IrcConstants.AUTHENTICATE, "+");
            return;
        }

        if (param == "*")
        {
            // Client is aborting authentication
            await session.SendNumericAsync(_server.Config.ServerName, Numerics.ERR_SASLABORTED,
                "SASL authentication aborted");
            return;
        }

        // This should be the base64-encoded PLAIN credentials
        // Format: \0username\0password (base64 encoded)
        byte[] decoded;
        try
        {
            decoded = Convert.FromBase64String(param);
        }
        catch (FormatException)
        {
            await session.SendNumericAsync(_server.Config.ServerName, Numerics.ERR_SASLFAIL,
                "SASL authentication failed");
            return;
        }

        // Parse PLAIN: authzid\0authcid\0password
        var parts = SplitPlainCredentials(decoded);
        if (parts == null || parts.Value.username.Length == 0)
        {
            await session.SendNumericAsync(_server.Config.ServerName, Numerics.ERR_SASLFAIL,
                "SASL authentication failed");
            return;
        }

        var (_, username, password) = parts.Value;

        // Validate credentials against the database
        using var scope = _scopeFactory.CreateScope();
        var accountRepo = scope.ServiceProvider.GetRequiredService<IUserAccountRepository>();
        var account = await accountRepo.GetByAccountAsync(username, ct);

        if (account == null || !PasswordHasher.VerifyPassword(password, account.PasswordHash))
        {
            await session.SendNumericAsync(_server.Config.ServerName, Numerics.ERR_SASLFAIL,
                "SASL authentication failed");
            return;
        }

        // Set the account name
        session.Info.Account = username;

        // Update last login timestamp
        await accountRepo.UpdateLastLoginAsync(username, DateTimeOffset.UtcNow, ct);

        // RPL_LOGGEDIN: <nick>!<user>@<host> <account> :You are now logged in as <username>
        var prefix = session.Info.Prefix;
        await session.SendNumericAsync(_server.Config.ServerName, Numerics.RPL_LOGGEDIN,
            prefix, username, $"You are now logged in as {username}");

        await session.SendNumericAsync(_server.Config.ServerName, Numerics.RPL_SASLSUCCESS,
            "SASL authentication successful");
    }

    private static (string authzid, string username, string password)? SplitPlainCredentials(byte[] data)
    {
        // PLAIN format: authzid\0authcid\0passwd
        var str = Encoding.UTF8.GetString(data);
        var firstNull = str.IndexOf('\0');
        if (firstNull < 0) return null;

        var secondNull = str.IndexOf('\0', firstNull + 1);
        if (secondNull < 0) return null;

        return (
            str[..firstNull],
            str[(firstNull + 1)..secondNull],
            str[(secondNull + 1)..]
        );
    }
}
