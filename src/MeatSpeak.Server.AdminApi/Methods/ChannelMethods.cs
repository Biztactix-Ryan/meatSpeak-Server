namespace MeatSpeak.Server.AdminApi.Methods;

using System.Text.Json;
using MeatSpeak.Server.Core.Server;

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
        if (parameters == null)
            throw new JsonException("Missing parameters");

        var chanName = parameters.Value.GetProperty("channel").GetString()
            ?? throw new JsonException("Missing 'channel'");

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
        if (parameters == null)
            throw new JsonException("Missing parameters");

        var chanName = parameters.Value.GetProperty("channel").GetString()
            ?? throw new JsonException("Missing 'channel'");
        var topic = parameters.Value.GetProperty("topic").GetString()
            ?? throw new JsonException("Missing 'topic'");

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
        if (parameters == null)
            throw new JsonException("Missing parameters");

        var chanName = parameters.Value.GetProperty("channel").GetString()
            ?? throw new JsonException("Missing 'channel'");
        var modes = parameters.Value.GetProperty("modes").GetString()
            ?? throw new JsonException("Missing 'modes'");

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
        if (parameters == null)
            throw new JsonException("Missing parameters");

        var chanName = parameters.Value.GetProperty("channel").GetString()
            ?? throw new JsonException("Missing 'channel'");

        var channel = _server.GetOrCreateChannel(chanName);

        if (parameters.Value.TryGetProperty("topic", out var topicEl))
        {
            channel.Topic = topicEl.GetString();
            channel.TopicSetBy = _server.Config.ServerName;
            channel.TopicSetAt = DateTimeOffset.UtcNow;
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
        if (parameters == null)
            throw new JsonException("Missing parameters");

        var chanName = parameters.Value.GetProperty("channel").GetString()
            ?? throw new JsonException("Missing 'channel'");

        string? reason = null;
        if (parameters.Value.TryGetProperty("reason", out var reasonEl))
            reason = reasonEl.GetString();

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
