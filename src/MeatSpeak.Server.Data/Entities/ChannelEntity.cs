namespace MeatSpeak.Server.Data.Entities;

public sealed class ChannelEntity
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Topic { get; set; }
    public string? TopicSetBy { get; set; }
    public DateTimeOffset? TopicSetAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public string? Key { get; set; }
    public int? UserLimit { get; set; }
    public string Modes { get; set; } = string.Empty;
}
