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
        if (await HandlerGuards.CheckNeedMoreParams(session, _server.Config.ServerName, message, 1, IrcConstants.JOIN))
            return;

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

            // Check per-user channel limit
            if (session.Info.Channels.Count >= _server.Config.MaxChannelsPerUser)
            {
                await session.SendNumericAsync(_server.Config.ServerName, Numerics.ERR_TOOMANYCHANNELS,
                    name, "You have joined too many channels");
                return;
            }

            var key = i < keys.Length ? keys[i] : null;
            await JoinChannel(session, name, key);
        }
    }

    private async ValueTask JoinChannel(ISession session, string name, string? key)
    {
        var nick = session.Info.Nickname!;
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

        // Add member first, then determine operator status atomically via member count.
        // ConcurrentDictionary.TryAdd ensures only one joiner can be sole member.
        var membership = new ChannelMembership { Nickname = nick };
        channel.AddMember(nick, membership);
        if (channel.Members.Count == 1)
            membership.IsOperator = true;

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
        ChatLogHelper.LogChannelEvent(_writeQueue, name, nick, string.Empty, IrcConstants.JOIN);

        // Persist new channel creation to database (first joiner triggers persist)
        if (channel.Members.Count == 1)
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

        // IRC max line = 512 bytes. Overhead: ":server 353 target = #channel :\r\n"
        var target = session.Info.Nickname ?? "*";
        int overhead = 1 + serverName.Length + 1 + 3 + 1 + target.Length + 1 + 1 + 1 + channel.Name.Length + 2 + 2; // :server 353 target = #chan :\r\n
        int maxPayload = IrcConstants.MaxLineLength - overhead;
        if (maxPayload < 32) maxPayload = 32;

        var batch = new System.Text.StringBuilder(maxPayload);
        foreach (var m in channel.Members)
        {
            var prefix = multiPrefix ? m.Value.AllPrefixChars : m.Value.PrefixChar;
            string entry;
            if (userhostInNames)
            {
                var memberSession = server.FindSessionByNick(m.Key);
                if (memberSession != null)
                    entry = $"{prefix}{memberSession.Info.Nickname}!{memberSession.Info.Username}@{memberSession.Info.Hostname}";
                else
                    entry = $"{prefix}{m.Key}";
            }
            else
            {
                entry = $"{prefix}{m.Key}";
            }

            if (batch.Length > 0 && batch.Length + 1 + entry.Length > maxPayload)
            {
                await session.SendNumericAsync(serverName, Numerics.RPL_NAMREPLY,
                    "=", channel.Name, batch.ToString());
                batch.Clear();
            }

            if (batch.Length > 0) batch.Append(' ');
            batch.Append(entry);
        }

        if (batch.Length > 0)
        {
            await session.SendNumericAsync(serverName, Numerics.RPL_NAMREPLY,
                "=", channel.Name, batch.ToString());
        }

        await session.SendNumericAsync(serverName, Numerics.RPL_ENDOFNAMES,
            channel.Name, "End of /NAMES list");
    }
}
