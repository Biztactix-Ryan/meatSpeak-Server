namespace MeatSpeak.Server.Data;

using System.Threading.Channels;
using MeatSpeak.Server.Data.Entities;

public abstract record DbWriteItem;

public sealed record AddChatLog(ChatLogEntity Entity) : DbWriteItem;
public sealed record AddUserHistory(UserHistoryEntity Entity) : DbWriteItem;
public sealed record UpdateUserDisconnect(string Nickname, DateTimeOffset DisconnectedAt, string? Reason) : DbWriteItem;
public sealed record UpsertChannel(ChannelEntity Entity) : DbWriteItem;
public sealed record DeleteChannel(string Name) : DbWriteItem;
public sealed record AddTopicHistory(TopicHistoryEntity Entity) : DbWriteItem;

public sealed class DbWriteQueue
{
    private readonly Channel<DbWriteItem> _channel = Channel.CreateBounded<DbWriteItem>(
        new BoundedChannelOptions(10_000)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
        });

    public ChannelReader<DbWriteItem> Reader => _channel.Reader;

    public bool TryWrite(DbWriteItem item) => _channel.Writer.TryWrite(item);

    public void Complete() => _channel.Writer.TryComplete();
}
