namespace MeatSpeak.Server.Core.Channels;

public sealed class ChannelMembership
{
    public string Nickname { get; set; } = string.Empty;
    public bool IsOperator { get; set; }
    public bool HasVoice { get; set; }
    public DateTimeOffset JoinedAt { get; set; } = DateTimeOffset.UtcNow;

    public string PrefixChar => IsOperator ? "@" : HasVoice ? "+" : "";

    /// <summary>All prefix characters for multi-prefix cap (e.g., "@+" for op+voice).</summary>
    public string AllPrefixChars => (IsOperator ? "@" : "") + (HasVoice ? "+" : "");
}
