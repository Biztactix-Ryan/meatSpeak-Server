namespace MeatSpeak.Server.Data.Entities;

public sealed class ChatLogEntity
{
    public long Id { get; set; }
    public string? ChannelName { get; set; }
    public string? Target { get; set; }
    public string Sender { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string MessageType { get; set; } = "PRIVMSG";
    public DateTimeOffset SentAt { get; set; } = DateTimeOffset.UtcNow;
    public string? MsgId { get; set; }
    public bool IsRedacted { get; set; }
    public string? RedactedBy { get; set; }
    public DateTimeOffset? RedactedAt { get; set; }
}
