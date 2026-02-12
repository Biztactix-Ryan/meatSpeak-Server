namespace MeatSpeak.Server.Handlers.Channels;

using MeatSpeak.Protocol;
using MeatSpeak.Server.Core.Commands;
using MeatSpeak.Server.Core.Events;
using MeatSpeak.Server.Core.Sessions;
using MeatSpeak.Server.Core.Server;
using MeatSpeak.Server.Data;
using MeatSpeak.Server.Data.Entities;

[FloodPenalty(2)]
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
        if (await HandlerGuards.CheckNeedMoreParams(session, _server.Config.ServerName, message, 1, IrcConstants.TOPIC))
            return;

        var channelName = message.GetParam(0)!;

        var channel = await HandlerGuards.RequireChannel(session, _server.Config.ServerName, _server, channelName);
        if (channel == null)
            return;

        if (await HandlerGuards.CheckNotOnChannel(session, _server.Config.ServerName, channel, session.Info.Nickname!))
            return;

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
            if (await HandlerGuards.CheckChanOpPrivsNeeded(session, _server.Config.ServerName, channel, session.Info.Nickname!))
                return;
        }

        var newTopic = message.GetParam(1)!;
        channel.Topic = newTopic;
        channel.TopicSetBy = session.Info.Nickname;
        channel.TopicSetAt = DateTimeOffset.UtcNow;

        // Broadcast TOPIC change to all channel members
        await ChannelBroadcaster.BroadcastToChannel(_server, channel, session.Info.Prefix, IrcConstants.TOPIC, channelName, newTopic);

        _server.Events.Publish(new TopicChangedEvent(channelName, newTopic, session.Info.Nickname!));

        // Log TOPIC event for chathistory event-playback
        ChatLogHelper.LogChannelEvent(_writeQueue, channelName, session.Info.Nickname!, newTopic, IrcConstants.TOPIC);

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
