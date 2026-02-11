namespace MeatSpeak.Server.Core.Channels;

public sealed record BanEntry(string Mask, string SetBy, DateTimeOffset SetAt);
