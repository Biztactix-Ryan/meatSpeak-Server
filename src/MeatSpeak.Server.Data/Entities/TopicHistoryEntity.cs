namespace MeatSpeak.Server.Data.Entities;

public sealed class TopicHistoryEntity
{
    public long Id { get; set; }
    public string ChannelName { get; set; } = string.Empty;
    public string Topic { get; set; } = string.Empty;
    public string SetBy { get; set; } = string.Empty;
    public DateTimeOffset SetAt { get; set; } = DateTimeOffset.UtcNow;
}
