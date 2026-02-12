namespace MeatSpeak.Server.Data.Entities;

public sealed class ReactionEntity
{
    public long Id { get; set; }
    public string MsgId { get; set; } = string.Empty;
    public string Sender { get; set; } = string.Empty;
    public string Reaction { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
