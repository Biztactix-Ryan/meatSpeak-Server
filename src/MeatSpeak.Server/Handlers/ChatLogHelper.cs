namespace MeatSpeak.Server.Handlers;

using MeatSpeak.Server.Capabilities;
using MeatSpeak.Server.Data;
using MeatSpeak.Server.Data.Entities;

internal static class ChatLogHelper
{
    /// <summary>
    /// Logs a channel event (JOIN, PART, QUIT, TOPIC, KICK) with an auto-generated MsgId.
    /// </summary>
    public static void LogChannelEvent(DbWriteQueue? q,
        string channel, string sender, string message, string messageType)
    {
        q?.TryWrite(new AddChatLog(new ChatLogEntity
        {
            ChannelName = channel,
            Sender = sender,
            Message = message,
            MessageType = messageType,
            SentAt = DateTimeOffset.UtcNow,
            MsgId = MsgIdGenerator.Generate(),
        }));
    }

    /// <summary>
    /// Logs a message (PRIVMSG, NOTICE) with a pre-generated MsgId.
    /// </summary>
    public static void LogMessage(DbWriteQueue? q,
        string sender, string? channel, string? target,
        string message, string messageType, string msgId)
    {
        q?.TryWrite(new AddChatLog(new ChatLogEntity
        {
            ChannelName = channel,
            Target = target,
            Sender = sender,
            Message = message,
            MessageType = messageType,
            SentAt = DateTimeOffset.UtcNow,
            MsgId = msgId,
        }));
    }
}
