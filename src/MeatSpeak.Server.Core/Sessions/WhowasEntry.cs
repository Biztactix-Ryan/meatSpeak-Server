namespace MeatSpeak.Server.Core.Sessions;

public sealed record WhowasEntry(
    string Nickname,
    string Username,
    string Hostname,
    string Realname,
    DateTimeOffset Timestamp);
