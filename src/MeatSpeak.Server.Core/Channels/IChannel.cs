namespace MeatSpeak.Server.Core.Channels;

public interface IChannel
{
    string Name { get; }
    string? Topic { get; set; }
    string? TopicSetBy { get; set; }
    DateTimeOffset? TopicSetAt { get; set; }
    DateTimeOffset CreatedAt { get; }
    IReadOnlyDictionary<string, ChannelMembership> Members { get; }
    HashSet<char> Modes { get; }
    string? Key { get; set; }
    int? UserLimit { get; set; }
    IReadOnlyList<BanEntry> Bans { get; }
    IReadOnlyList<string> InviteList { get; }

    bool AddMember(string nickname, ChannelMembership membership);
    bool RemoveMember(string nickname);
    ChannelMembership? GetMember(string nickname);
    bool IsMember(string nickname);
    void AddBan(BanEntry ban);
    bool RemoveBan(string mask);
    bool IsBanned(string mask);
    void AddInvite(string nickname);
    bool IsInvited(string nickname);
}
