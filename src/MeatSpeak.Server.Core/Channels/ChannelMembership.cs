namespace MeatSpeak.Server.Core.Channels;

public sealed class ChannelMembership
{
    public string Nickname { get; set; } = string.Empty;
    public bool IsOperator { get; set; }
    public bool HasVoice { get; set; }
    public DateTimeOffset JoinedAt { get; set; } = DateTimeOffset.UtcNow;

    public string PrefixChar => IsOperator ? "@" : HasVoice ? "+" : "";
}
