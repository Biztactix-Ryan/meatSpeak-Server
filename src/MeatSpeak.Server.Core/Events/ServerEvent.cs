namespace MeatSpeak.Server.Core.Events;

public abstract record ServerEvent(DateTimeOffset Timestamp)
{
    protected ServerEvent() : this(DateTimeOffset.UtcNow) { }
}

public sealed record SessionConnectedEvent(string SessionId) : ServerEvent;
public sealed record SessionDisconnectedEvent(string SessionId, string? Reason) : ServerEvent;
public sealed record SessionRegisteredEvent(string SessionId, string Nickname) : ServerEvent;
public sealed record SessionAuthenticatedEvent(string SessionId, string Account) : ServerEvent;
public sealed record NickChangedEvent(string SessionId, string OldNick, string NewNick) : ServerEvent;
public sealed record ChannelJoinedEvent(string SessionId, string Nickname, string Channel) : ServerEvent;
public sealed record ChannelPartedEvent(string SessionId, string Nickname, string Channel, string? Reason) : ServerEvent;
public sealed record ChannelMessageEvent(string SessionId, string Nickname, string Channel, string Message) : ServerEvent;
public sealed record PrivateMessageEvent(string SessionId, string FromNick, string ToNick, string Message) : ServerEvent;
public sealed record TopicChangedEvent(string Channel, string? Topic, string SetBy) : ServerEvent;
public sealed record ModeChangedEvent(string Target, string ModeString, string SetBy) : ServerEvent;
public sealed record RoleAssignedEvent(string Account, Guid RoleId) : ServerEvent;
public sealed record RoleRevokedEvent(string Account, Guid RoleId) : ServerEvent;
public sealed record RoleUpdatedEvent(Guid RoleId) : ServerEvent;
public sealed record ServerRehashEvent() : ServerEvent;
