namespace MeatSpeak.Server.Handlers.Channels;

using MeatSpeak.Protocol;
using MeatSpeak.Server.Core.Channels;
using MeatSpeak.Server.Core.Commands;
using MeatSpeak.Server.Core.Events;
using MeatSpeak.Server.Core.Sessions;
using MeatSpeak.Server.Core.Server;
using MeatSpeak.Server.Capabilities;
using MeatSpeak.Server.Data;
using MeatSpeak.Server.Data.Entities;
using MeatSpeak.Server.Diagnostics;

[FloodPenalty(2)]
public sealed class JoinHandler : ICommandHandler
{
    private readonly IServer _server;
    private readonly DbWriteQueue? _writeQueue;
    private readonly ServerMetrics? _metrics;
    public string Command => IrcConstants.JOIN;
    public SessionState MinimumState => SessionState.Registered;

    public JoinHandler(IServer server, DbWriteQueue? writeQueue = null, ServerMetrics? metrics = null)
    {
        _server = server;
        _writeQueue = writeQueue;
        _metrics = metrics;
    }

    public async ValueTask HandleAsync(ISession session, IrcMessage message, CancellationToken ct = default)
    {
        if (message.Parameters.Count < 1)
        {
            await session.SendNumericAsync(_server.Config.ServerName, Numerics.ERR_NEEDMOREPARAMS,
                IrcConstants.JOIN, "Not enough parameters");
            return;
        }

        // JOIN 0 = part all channels
        if (message.GetParam(0) == "0")
        {
            foreach (var channelName in session.Info.Channels.ToList())
            {
                if (_server.Channels.TryGetValue(channelName, out var ch))
                {
                    foreach (var (memberNick, _) in ch.Members)
                    {
                        var memberSession = _server.FindSessionByNick(memberNick);
                        if (memberSession != null)
                            await CapHelper.SendWithTimestamp(memberSession, session.Info.Prefix, IrcConstants.PART, channelName);
                    }
                    ch.RemoveMember(session.Info.Nickname!);
                    if (ch.Members.Count == 0)
                        _server.RemoveChannel(channelName);
                }
                session.Info.Channels.Remove(channelName);
            }
            return;
        }

        var channelNames = message.GetParam(0)!.Split(',');
        var keys = message.GetParam(1)?.Split(',') ?? Array.Empty<string>();

        for (int i = 0; i < channelNames.Length; i++)
        {
            var name = channelNames[i].Trim();
            if (string.IsNullOrEmpty(name))
                continue;

            if (!name.StartsWith('#'))
            {
                await session.SendNumericAsync(_server.Config.ServerName, Numerics.ERR_NOSUCHCHANNEL,
                    name, "No such channel");
                continue;
            }

            var key = i < keys.Length ? keys[i] : null;
            await JoinChannel(session, name, key);
        }
    }

    private async ValueTask JoinChannel(ISession session, string name, string? key)
    {
        var nick = session.Info.Nickname!;
        var isNew = !_server.Channels.TryGetValue(name, out _);
        var channel = _server.GetOrCreateChannel(name);

        if (channel.IsMember(nick))
            return;

        // Check channel key
        if (channel.Key != null && !string.Equals(channel.Key, key, StringComparison.Ordinal))
        {
            await session.SendNumericAsync(_server.Config.ServerName, Numerics.ERR_BADCHANNELKEY,
                name, "Cannot join channel (+k)");
            return;
        }

        // Check invite-only
        if (channel.Modes.Contains('i') && !channel.IsInvited(nick))
        {
            await session.SendNumericAsync(_server.Config.ServerName, Numerics.ERR_INVITEONLYCHAN,
                name, "Cannot join channel (+i)");
            return;
        }

        // Check ban list (skip if user matches an exception)
        if (channel.IsBanned(session.Info.Prefix) && !channel.IsExcepted(session.Info.Prefix))
        {
            await session.SendNumericAsync(_server.Config.ServerName, Numerics.ERR_BANNEDFROMCHAN,
                name, "Cannot join channel (+b)");
            return;
        }

        // Check user limit
        if (channel.UserLimit.HasValue && channel.Members.Count >= channel.UserLimit.Value)
        {
            await session.SendNumericAsync(_server.Config.ServerName, Numerics.ERR_CHANNELISFULL,
                name, "Cannot join channel (+l)");
            return;
        }

        // First joiner gets operator status
        var membership = new ChannelMembership
        {
            Nickname = nick,
            IsOperator = isNew || channel.Members.Count == 0,
        };

        channel.AddMember(nick, membership);
        session.Info.Channels.Add(name);

        // Broadcast JOIN to all channel members (including sender)
        var broadcastStart = ServerMetrics.GetTimestamp();
        foreach (var (memberNick, _) in channel.Members)
        {
            var memberSession = _server.FindSessionByNick(memberNick);
            if (memberSession != null)
            {
                if (CapHelper.HasCap(memberSession, "extended-join"))
                    await CapHelper.SendWithTimestamp(memberSession, session.Info.Prefix, IrcConstants.JOIN, name, session.Info.Account ?? "*", session.Info.Realname ?? "");
                else
                    await CapHelper.SendWithTimestamp(memberSession, session.Info.Prefix, IrcConstants.JOIN, name);
            }
        }
        _metrics?.RecordBroadcastDuration(ServerMetrics.GetElapsedMs(broadcastStart));

        // Send topic
        if (!string.IsNullOrEmpty(channel.Topic))
        {
            await session.SendNumericAsync(_server.Config.ServerName, Numerics.RPL_TOPIC,
                name, channel.Topic);
            if (channel.TopicSetBy != null && channel.TopicSetAt.HasValue)
            {
                await session.SendNumericAsync(_server.Config.ServerName, Numerics.RPL_TOPICWHOTIME,
                    name, channel.TopicSetBy, channel.TopicSetAt.Value.ToUnixTimeSeconds().ToString());
            }
        }
        else
        {
            await session.SendNumericAsync(_server.Config.ServerName, Numerics.RPL_NOTOPIC,
                name, "No topic is set");
        }

        // Send NAMES list
        await SendNamesReply(session, channel, _server.Config.ServerName, _server);

        _server.Events.Publish(new ChannelJoinedEvent(session.Id, nick, name));

        // Log JOIN event for chathistory event-playback
        _writeQueue?.TryWrite(new AddChatLog(new ChatLogEntity
        {
            ChannelName = name,
            Sender = nick,
            Message = string.Empty,
            MessageType = IrcConstants.JOIN,
            SentAt = DateTimeOffset.UtcNow,
            MsgId = Capabilities.MsgIdGenerator.Generate(),
        }));

        // Persist new channel creation to database
        if (isNew)
        {
            _writeQueue?.TryWrite(new UpsertChannel(new ChannelEntity
            {
                Id = Guid.NewGuid(),
                Name = channel.Name,
                Topic = channel.Topic,
                TopicSetBy = channel.TopicSetBy,
                TopicSetAt = channel.TopicSetAt,
                CreatedAt = channel.CreatedAt,
                Key = channel.Key,
                UserLimit = channel.UserLimit,
                Modes = new string(channel.Modes.ToArray()),
            }));
        }
    }

    internal static async ValueTask SendNamesReply(ISession session, IChannel channel, string serverName, IServer server)
    {
        var multiPrefix = CapHelper.HasCap(session, "multi-prefix");
        var userhostInNames = CapHelper.HasCap(session, "userhost-in-names");

        var names = string.Join(" ", channel.Members.Select(m =>
        {
            var prefix = multiPrefix ? m.Value.AllPrefixChars : m.Value.PrefixChar;
            if (userhostInNames)
            {
                var memberSession = server.FindSessionByNick(m.Key);
                if (memberSession != null)
                    return $"{prefix}{memberSession.Info.Nickname}!{memberSession.Info.Username}@{memberSession.Info.Hostname}";
            }
            return $"{prefix}{m.Key}";
        }));

        await session.SendNumericAsync(serverName, Numerics.RPL_NAMREPLY,
            "=", channel.Name, names);
        await session.SendNumericAsync(serverName, Numerics.RPL_ENDOFNAMES,
            channel.Name, "End of /NAMES list");
    }
}
