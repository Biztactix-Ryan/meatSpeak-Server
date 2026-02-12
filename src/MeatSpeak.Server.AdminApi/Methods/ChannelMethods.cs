namespace MeatSpeak.Server.AdminApi.Methods;

using System.Text.Json;
using MeatSpeak.Server.Core.Channels;
using MeatSpeak.Server.Core.Server;
using MeatSpeak.Server.Permissions;

public sealed class ChannelListMethod : IAdminMethod
{
    private readonly IServer _server;
    public string Name => "channel.list";
    public ChannelListMethod(IServer server) => _server = server;

    public Task<object?> ExecuteAsync(JsonElement? parameters, CancellationToken ct = default)
    {
        var channels = _server.Channels.Values.Select(c => new
        {
            name = c.Name,
            members = c.Members.Count,
            topic = c.Topic,
        }).ToList();
        return Task.FromResult<object?>(new { channels });
    }
}

public sealed class ChannelInfoMethod : IAdminMethod
{
    private readonly IServer _server;
    public string Name => "channel.info";
    public ChannelInfoMethod(IServer server) => _server = server;

    public Task<object?> ExecuteAsync(JsonElement? parameters, CancellationToken ct = default)
    {
        var p = AdminParamHelper.Require(parameters);
        var chanName = AdminParamHelper.RequireString(p, "channel");

        if (!_server.Channels.TryGetValue(chanName, out var channel))
            return Task.FromResult<object?>(new { error = "channel_not_found" });

        return Task.FromResult<object?>(new
        {
            name = channel.Name,
            topic = channel.Topic,
            topic_set_by = channel.TopicSetBy,
            topic_set_at = channel.TopicSetAt,
            modes = new string(channel.Modes.ToArray()),
            members = channel.Members.Values.Select(m => new
            {
                nick = m.Nickname,
                prefix = m.PrefixChar,
                is_operator = m.IsOperator,
                has_voice = m.HasVoice,
            }).ToList(),
            bans = channel.Bans.Select(b => new
            {
                mask = b.Mask,
                set_by = b.SetBy,
                set_at = b.SetAt,
            }).ToList(),
            created_at = channel.CreatedAt,
            key = channel.Key,
            user_limit = channel.UserLimit,
        });
    }
}

public sealed class ChannelTopicMethod : IAdminMethod
{
    private readonly IServer _server;
    public string Name => "channel.topic";
    public ChannelTopicMethod(IServer server) => _server = server;

    public async Task<object?> ExecuteAsync(JsonElement? parameters, CancellationToken ct = default)
    {
        var p = AdminParamHelper.Require(parameters);
        var chanName = AdminParamHelper.RequireString(p, "channel");
        var topic = AdminParamHelper.RequireString(p, "topic");

        if (!_server.Channels.TryGetValue(chanName, out var channel))
            return new { error = "channel_not_found" };

        channel.Topic = topic;
        channel.TopicSetBy = _server.Config.ServerName;
        channel.TopicSetAt = DateTimeOffset.UtcNow;

        // Broadcast TOPIC to members
        foreach (var member in channel.Members.Values)
        {
            var session = _server.FindSessionByNick(member.Nickname);
            if (session != null)
                await session.SendMessageAsync(_server.Config.ServerName, "TOPIC", chanName, topic);
        }

        return new { status = "ok" };
    }
}

public sealed class ChannelModeMethod : IAdminMethod
{
    private readonly IServer _server;
    public string Name => "channel.mode";
    public ChannelModeMethod(IServer server) => _server = server;

    public Task<object?> ExecuteAsync(JsonElement? parameters, CancellationToken ct = default)
    {
        var p = AdminParamHelper.Require(parameters);
        var chanName = AdminParamHelper.RequireString(p, "channel");
        var modes = AdminParamHelper.RequireString(p, "modes");

        if (!_server.Channels.TryGetValue(chanName, out var channel))
            return Task.FromResult<object?>(new { error = "channel_not_found" });

        // Parse simple +/- modes
        bool adding = true;
        foreach (var c in modes)
        {
            if (c == '+') { adding = true; continue; }
            if (c == '-') { adding = false; continue; }
            if (adding)
                channel.Modes.Add(c);
            else
                channel.Modes.Remove(c);
        }

        return Task.FromResult<object?>(new { status = "ok", modes = new string(channel.Modes.ToArray()) });
    }
}

public sealed class ChannelCreateMethod : IAdminMethod
{
    private readonly IServer _server;
    public string Name => "channel.create";
    public ChannelCreateMethod(IServer server) => _server = server;

    public Task<object?> ExecuteAsync(JsonElement? parameters, CancellationToken ct = default)
    {
        var p = AdminParamHelper.Require(parameters);
        var chanName = AdminParamHelper.RequireString(p, "channel");

        var channel = _server.GetOrCreateChannel(chanName);

        if (p.TryGetProperty("topic", out var topicEl))
        {
            channel.Topic = topicEl.GetString();
            channel.TopicSetBy = _server.Config.ServerName;
            channel.TopicSetAt = DateTimeOffset.UtcNow;
        }

        if (p.TryGetProperty("modes", out var modesEl))
        {
            var modes = modesEl.GetString();
            if (modes != null)
            {
                bool adding = true;
                foreach (var c in modes)
                {
                    if (c == '+') { adding = true; continue; }
                    if (c == '-') { adding = false; continue; }
                    if (adding)
                        channel.Modes.Add(c);
                    else
                        channel.Modes.Remove(c);
                }
            }
        }

        if (p.TryGetProperty("key", out var keyEl))
        {
            channel.Key = keyEl.GetString();
            if (channel.Key != null)
                channel.Modes.Add('k');
        }

        if (p.TryGetProperty("user_limit", out var limitEl) && limitEl.TryGetInt32(out var limit))
        {
            channel.UserLimit = limit > 0 ? limit : null;
            if (channel.UserLimit.HasValue)
                channel.Modes.Add('l');
            else
                channel.Modes.Remove('l');
        }

        return Task.FromResult<object?>(new { status = "ok", channel = chanName });
    }
}

public sealed class ChannelDeleteMethod : IAdminMethod
{
    private readonly IServer _server;
    public string Name => "channel.delete";
    public ChannelDeleteMethod(IServer server) => _server = server;

    public async Task<object?> ExecuteAsync(JsonElement? parameters, CancellationToken ct = default)
    {
        var p = AdminParamHelper.Require(parameters);
        var chanName = AdminParamHelper.RequireString(p, "channel");
        var reason = AdminParamHelper.OptionalString(p, "reason");

        if (!_server.Channels.TryGetValue(chanName, out var channel))
            return new { error = "channel_not_found" };

        var kickMsg = reason ?? "Channel deleted by admin";

        // Kick all members
        foreach (var member in channel.Members.Values.ToList())
        {
            var session = _server.FindSessionByNick(member.Nickname);
            if (session != null)
            {
                await session.SendMessageAsync(_server.Config.ServerName, "KICK", chanName, member.Nickname, kickMsg);
                session.Info.Channels.Remove(chanName);
            }
        }

        _server.RemoveChannel(chanName);
        return new { status = "ok" };
    }
}

public sealed class ChannelPermissionsMethod : IAdminMethod
{
    private readonly IPermissionService _permissions;
    public string Name => "channel.permissions";
    public ChannelPermissionsMethod(IPermissionService permissions) => _permissions = permissions;

    public async Task<object?> ExecuteAsync(JsonElement? parameters, CancellationToken ct = default)
    {
        var p = AdminParamHelper.Require(parameters);
        var chanName = AdminParamHelper.RequireString(p, "channel");

        var overrides = await _permissions.GetChannelOverridesAsync(chanName, ct);
        var roles = await _permissions.GetAllRolesAsync(ct);
        var roleMap = roles.ToDictionary(r => r.Id);

        return new
        {
            channel = chanName,
            overrides = overrides.Select(o => new
            {
                role_id = o.RoleId,
                role_name = roleMap.TryGetValue(o.RoleId, out var r) ? r.Name : null,
                allow = (ulong)o.Allow,
                deny = (ulong)o.Deny,
            }).ToList(),
        };
    }
}

public sealed class ChannelPermissionsSetMethod : IAdminMethod
{
    private readonly IPermissionService _permissions;
    public string Name => "channel.permissions.set";
    public ChannelPermissionsSetMethod(IPermissionService permissions) => _permissions = permissions;

    public async Task<object?> ExecuteAsync(JsonElement? parameters, CancellationToken ct = default)
    {
        var p = AdminParamHelper.Require(parameters);
        var chanName = AdminParamHelper.RequireString(p, "channel");
        var roleId = p.GetProperty("role_id").GetGuid();
        var allow = p.TryGetProperty("allow", out var allowEl) ? allowEl.GetUInt64() : 0UL;
        var deny = p.TryGetProperty("deny", out var denyEl) ? denyEl.GetUInt64() : 0UL;

        await _permissions.SetChannelOverrideAsync(new ChannelOverride(roleId, chanName,
            (ChannelPermission)allow, (ChannelPermission)deny), ct);

        return new { status = "ok" };
    }
}

public sealed class ChannelPermissionsDeleteMethod : IAdminMethod
{
    private readonly IPermissionService _permissions;
    public string Name => "channel.permissions.delete";
    public ChannelPermissionsDeleteMethod(IPermissionService permissions) => _permissions = permissions;

    public async Task<object?> ExecuteAsync(JsonElement? parameters, CancellationToken ct = default)
    {
        var p = AdminParamHelper.Require(parameters);
        var chanName = AdminParamHelper.RequireString(p, "channel");
        var roleId = p.GetProperty("role_id").GetGuid();

        await _permissions.DeleteChannelOverrideAsync(roleId, chanName, ct);

        return new { status = "ok" };
    }
}

public sealed class ChannelMemberModeMethod : IAdminMethod
{
    private readonly IServer _server;
    public string Name => "channel.member.mode";
    public ChannelMemberModeMethod(IServer server) => _server = server;

    public async Task<object?> ExecuteAsync(JsonElement? parameters, CancellationToken ct = default)
    {
        var p = AdminParamHelper.Require(parameters);
        var chanName = AdminParamHelper.RequireString(p, "channel");
        var nick = AdminParamHelper.RequireString(p, "nick");
        var modes = AdminParamHelper.RequireString(p, "modes");

        if (!_server.Channels.TryGetValue(chanName, out var channel))
            return new { error = "channel_not_found" };

        var membership = channel.GetMember(nick);
        if (membership == null)
            return new { error = "user_not_in_channel" };

        bool adding = true;
        var applied = new List<string>();
        foreach (var c in modes)
        {
            if (c == '+') { adding = true; continue; }
            if (c == '-') { adding = false; continue; }
            switch (c)
            {
                case 'o':
                    membership.IsOperator = adding;
                    applied.Add((adding ? "+" : "-") + "o");
                    break;
                case 'v':
                    membership.HasVoice = adding;
                    applied.Add((adding ? "+" : "-") + "v");
                    break;
            }
        }

        if (applied.Count > 0)
        {
            // Broadcast MODE change to channel members
            var modeStr = string.Join("", applied);
            foreach (var (memberNick, _) in channel.Members)
            {
                var session = _server.FindSessionByNick(memberNick);
                if (session != null)
                    await session.SendMessageAsync(_server.Config.ServerName, "MODE", chanName, modeStr, nick);
            }
        }

        return new { status = "ok", is_operator = membership.IsOperator, has_voice = membership.HasVoice };
    }
}

public sealed class ChannelBansMethod : IAdminMethod
{
    private readonly IServer _server;
    public string Name => "channel.bans";
    public ChannelBansMethod(IServer server) => _server = server;

    public Task<object?> ExecuteAsync(JsonElement? parameters, CancellationToken ct = default)
    {
        var p = AdminParamHelper.Require(parameters);
        var chanName = AdminParamHelper.RequireString(p, "channel");

        if (!_server.Channels.TryGetValue(chanName, out var channel))
            return Task.FromResult<object?>(new { error = "channel_not_found" });

        return Task.FromResult<object?>(new
        {
            channel = chanName,
            bans = channel.Bans.Select(b => new
            {
                mask = b.Mask,
                set_by = b.SetBy,
                set_at = b.SetAt,
            }).ToList(),
        });
    }
}

public sealed class ChannelBansAddMethod : IAdminMethod
{
    private readonly IServer _server;
    public string Name => "channel.bans.add";
    public ChannelBansAddMethod(IServer server) => _server = server;

    public Task<object?> ExecuteAsync(JsonElement? parameters, CancellationToken ct = default)
    {
        var p = AdminParamHelper.Require(parameters);
        var chanName = AdminParamHelper.RequireString(p, "channel");
        var mask = AdminParamHelper.RequireString(p, "mask");
        var setBy = AdminParamHelper.OptionalString(p, "set_by") ?? "admin";

        if (!_server.Channels.TryGetValue(chanName, out var channel))
            return Task.FromResult<object?>(new { error = "channel_not_found" });

        channel.AddBan(new BanEntry(mask, setBy, DateTimeOffset.UtcNow));
        return Task.FromResult<object?>(new { status = "ok" });
    }
}

public sealed class ChannelBansRemoveMethod : IAdminMethod
{
    private readonly IServer _server;
    public string Name => "channel.bans.remove";
    public ChannelBansRemoveMethod(IServer server) => _server = server;

    public Task<object?> ExecuteAsync(JsonElement? parameters, CancellationToken ct = default)
    {
        var p = AdminParamHelper.Require(parameters);
        var chanName = AdminParamHelper.RequireString(p, "channel");
        var mask = AdminParamHelper.RequireString(p, "mask");

        if (!_server.Channels.TryGetValue(chanName, out var channel))
            return Task.FromResult<object?>(new { error = "channel_not_found" });

        var removed = channel.RemoveBan(mask);
        return Task.FromResult<object?>(new { status = removed ? "ok" : "not_found" });
    }
}

public sealed class ChannelExceptsMethod : IAdminMethod
{
    private readonly IServer _server;
    public string Name => "channel.excepts";
    public ChannelExceptsMethod(IServer server) => _server = server;

    public Task<object?> ExecuteAsync(JsonElement? parameters, CancellationToken ct = default)
    {
        var p = AdminParamHelper.Require(parameters);
        var chanName = AdminParamHelper.RequireString(p, "channel");

        if (!_server.Channels.TryGetValue(chanName, out var channel))
            return Task.FromResult<object?>(new { error = "channel_not_found" });

        return Task.FromResult<object?>(new
        {
            channel = chanName,
            excepts = channel.Excepts.Select(e => new
            {
                mask = e.Mask,
                set_by = e.SetBy,
                set_at = e.SetAt,
            }).ToList(),
        });
    }
}

public sealed class ChannelExceptsAddMethod : IAdminMethod
{
    private readonly IServer _server;
    public string Name => "channel.excepts.add";
    public ChannelExceptsAddMethod(IServer server) => _server = server;

    public Task<object?> ExecuteAsync(JsonElement? parameters, CancellationToken ct = default)
    {
        var p = AdminParamHelper.Require(parameters);
        var chanName = AdminParamHelper.RequireString(p, "channel");
        var mask = AdminParamHelper.RequireString(p, "mask");
        var setBy = AdminParamHelper.OptionalString(p, "set_by") ?? "admin";

        if (!_server.Channels.TryGetValue(chanName, out var channel))
            return Task.FromResult<object?>(new { error = "channel_not_found" });

        channel.AddExcept(new BanEntry(mask, setBy, DateTimeOffset.UtcNow));
        return Task.FromResult<object?>(new { status = "ok" });
    }
}

public sealed class ChannelExceptsRemoveMethod : IAdminMethod
{
    private readonly IServer _server;
    public string Name => "channel.excepts.remove";
    public ChannelExceptsRemoveMethod(IServer server) => _server = server;

    public Task<object?> ExecuteAsync(JsonElement? parameters, CancellationToken ct = default)
    {
        var p = AdminParamHelper.Require(parameters);
        var chanName = AdminParamHelper.RequireString(p, "channel");
        var mask = AdminParamHelper.RequireString(p, "mask");

        if (!_server.Channels.TryGetValue(chanName, out var channel))
            return Task.FromResult<object?>(new { error = "channel_not_found" });

        var removed = channel.RemoveExcept(mask);
        return Task.FromResult<object?>(new { status = removed ? "ok" : "not_found" });
    }
}
