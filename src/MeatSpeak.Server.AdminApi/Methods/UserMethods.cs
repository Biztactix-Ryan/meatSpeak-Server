namespace MeatSpeak.Server.AdminApi.Methods;

using System.Text.Json;
using MeatSpeak.Server.Core.Server;
using MeatSpeak.Server.Core.Sessions;
using MeatSpeak.Server.Data.Entities;
using MeatSpeak.Server.Data.Repositories;

public sealed class UserListMethod : IAdminMethod
{
    private readonly IServer _server;
    public string Name => "user.list";
    public UserListMethod(IServer server) => _server = server;

    public Task<object?> ExecuteAsync(JsonElement? parameters, CancellationToken ct = default)
    {
        var users = _server.Sessions.Values.Select(s => new
        {
            id = s.Id,
            nick = s.Info.Nickname,
            account = s.Info.Account,
            state = s.State.ToString(),
        }).ToList();
        return Task.FromResult<object?>(new { users });
    }
}

public sealed class UserInfoMethod : IAdminMethod
{
    private readonly IServer _server;
    public string Name => "user.info";
    public UserInfoMethod(IServer server) => _server = server;

    public Task<object?> ExecuteAsync(JsonElement? parameters, CancellationToken ct = default)
    {
        if (parameters == null)
            throw new JsonException("Missing parameters");

        var nick = parameters.Value.GetProperty("nick").GetString()
            ?? throw new JsonException("Missing 'nick'");

        var session = _server.FindSessionByNick(nick);
        if (session == null)
            return Task.FromResult<object?>(new { error = "user_not_found" });

        return Task.FromResult<object?>(new
        {
            id = session.Id,
            nick = session.Info.Nickname,
            user = session.Info.Username,
            host = session.Info.Hostname,
            account = session.Info.Account,
            channels = session.Info.Channels.ToList(),
            modes = new string(session.Info.UserModes.ToArray()),
            connected_at = session.Info.ConnectedAt,
            idle_seconds = (DateTimeOffset.UtcNow - session.Info.LastActivity).TotalSeconds,
        });
    }
}

public sealed class UserKickMethod : IAdminMethod
{
    private readonly IServer _server;
    private readonly IAuditLogRepository? _audit;
    public string Name => "user.kick";

    public UserKickMethod(IServer server, IAuditLogRepository? audit = null)
    {
        _server = server;
        _audit = audit;
    }

    public async Task<object?> ExecuteAsync(JsonElement? parameters, CancellationToken ct = default)
    {
        if (parameters == null)
            throw new JsonException("Missing parameters");

        var nick = parameters.Value.GetProperty("nick").GetString()
            ?? throw new JsonException("Missing 'nick'");

        string? reason = null;
        if (parameters.Value.TryGetProperty("reason", out var reasonEl))
            reason = reasonEl.GetString();

        var session = _server.FindSessionByNick(nick);
        if (session == null)
            return new { error = "user_not_found" };

        // Notify channel members with QUIT
        var quitMsg = reason ?? "Kicked by admin";
        foreach (var chanName in session.Info.Channels.ToList())
        {
            if (_server.Channels.TryGetValue(chanName, out var channel))
            {
                foreach (var member in channel.Members.Values)
                {
                    if (!string.Equals(member.Nickname, nick, StringComparison.OrdinalIgnoreCase))
                    {
                        var memberSession = _server.FindSessionByNick(member.Nickname);
                        if (memberSession != null)
                            await memberSession.SendMessageAsync(session.Info.Prefix, "QUIT", quitMsg);
                    }
                }
                channel.RemoveMember(nick);
            }
        }

        await session.DisconnectAsync(quitMsg);

        if (_audit != null)
        {
            await _audit.AddAsync(new AuditLogEntity
            {
                Action = "user.kick",
                Actor = "admin-api",
                Target = nick,
                Details = reason,
            }, ct);
        }

        return new { status = "ok" };
    }
}

public sealed class UserMessageMethod : IAdminMethod
{
    private readonly IServer _server;
    public string Name => "user.message";
    public UserMessageMethod(IServer server) => _server = server;

    public async Task<object?> ExecuteAsync(JsonElement? parameters, CancellationToken ct = default)
    {
        if (parameters == null)
            throw new JsonException("Missing parameters");

        var nick = parameters.Value.GetProperty("nick").GetString()
            ?? throw new JsonException("Missing 'nick'");
        var message = parameters.Value.GetProperty("message").GetString()
            ?? throw new JsonException("Missing 'message'");

        var session = _server.FindSessionByNick(nick);
        if (session == null)
            return new { error = "user_not_found" };

        await session.SendMessageAsync(_server.Config.ServerName, "NOTICE", nick, message);
        return new { status = "ok" };
    }
}
