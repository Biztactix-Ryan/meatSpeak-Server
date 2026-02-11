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
}
