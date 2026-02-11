namespace MeatSpeak.Server.Handlers.Channels;

using MeatSpeak.Protocol;
using MeatSpeak.Server.Core.Commands;
using MeatSpeak.Server.Core.Events;
using MeatSpeak.Server.Core.Sessions;
using MeatSpeak.Server.Core.Server;
using MeatSpeak.Server.Data;
using MeatSpeak.Server.Data.Entities;

public sealed class TopicHandler : ICommandHandler
{
    private readonly IServer _server;
    private readonly DbWriteQueue? _writeQueue;
    public string Command => IrcConstants.TOPIC;
    public SessionState MinimumState => SessionState.Registered;

    public TopicHandler(IServer server, DbWriteQueue? writeQueue = null)
    {
        _server = server;
        _writeQueue = writeQueue;
    }

    public async ValueTask HandleAsync(ISession session, IrcMessage message, CancellationToken ct = default)
    {
        if (message.Parameters.Count < 1)
        {
            await session.SendNumericAsync(_server.Config.ServerName, Numerics.ERR_NEEDMOREPARAMS,
                IrcConstants.TOPIC, "Not enough parameters");
            return;
        }

        var channelName = message.GetParam(0)!;

        if (!_server.Channels.TryGetValue(channelName, out var channel))
        {
            await session.SendNumericAsync(_server.Config.ServerName, Numerics.ERR_NOSUCHCHANNEL,
                channelName, "No such channel");
            return;
        }

        if (!channel.IsMember(session.Info.Nickname!))
        {
            await session.SendNumericAsync(_server.Config.ServerName, Numerics.ERR_NOTONCHANNEL,
                channelName, "You're not on that channel");
            return;
        }

        if (message.Parameters.Count < 2)
        {
            // Query topic
            if (!string.IsNullOrEmpty(channel.Topic))
            {
                await session.SendNumericAsync(_server.Config.ServerName, Numerics.RPL_TOPIC,
                    channelName, channel.Topic);
                if (channel.TopicSetBy != null && channel.TopicSetAt.HasValue)
                {
                    await session.SendNumericAsync(_server.Config.ServerName, Numerics.RPL_TOPICWHOTIME,
                        channelName, channel.TopicSetBy, channel.TopicSetAt.Value.ToUnixTimeSeconds().ToString());
                }
            }
            else
            {
                await session.SendNumericAsync(_server.Config.ServerName, Numerics.RPL_NOTOPIC,
                    channelName, "No topic is set");
            }
            return;
        }

        // Set topic - check +t mode requires chanop
        if (channel.Modes.Contains('t'))
        {
            var membership = channel.GetMember(session.Info.Nickname!);
            if (membership == null || !membership.IsOperator)
            {
                await session.SendNumericAsync(_server.Config.ServerName, Numerics.ERR_CHANOPRIVSNEEDED,
                    channelName, "You're not channel operator");
                return;
            }
        }

        var newTopic = message.GetParam(1)!;
        channel.Topic = newTopic;
        channel.TopicSetBy = session.Info.Nickname;
        channel.TopicSetAt = DateTimeOffset.UtcNow;

        // Broadcast TOPIC change to all channel members
        foreach (var (memberNick, _) in channel.Members)
        {
            var memberSession = _server.FindSessionByNick(memberNick);
            if (memberSession != null)
                await memberSession.SendMessageAsync(session.Info.Prefix, IrcConstants.TOPIC, channelName, newTopic);
        }

        _server.Events.Publish(new TopicChangedEvent(channelName, newTopic, session.Info.Nickname!));

        // Persist topic change to database
        if (_writeQueue != null)
        {
            var now = DateTimeOffset.UtcNow;
            _writeQueue.TryWrite(new UpsertChannel(new ChannelEntity
            {
                Id = Guid.NewGuid(),
                Name = channelName,
                Topic = newTopic,
                TopicSetBy = session.Info.Nickname!,
                TopicSetAt = now,
                CreatedAt = now,
            }));
            _writeQueue.TryWrite(new AddTopicHistory(new TopicHistoryEntity
            {
                ChannelName = channelName,
                Topic = newTopic,
                SetBy = session.Info.Nickname!,
                SetAt = now,
            }));
        }
    }
}
