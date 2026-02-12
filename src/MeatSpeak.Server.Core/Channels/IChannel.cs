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
    IReadOnlyList<BanEntry> Excepts { get; }

    bool AddMember(string nickname, ChannelMembership membership);
    bool RemoveMember(string nickname);
    bool UpdateMemberNick(string oldNick, string newNick);
    ChannelMembership? GetMember(string nickname);
    bool IsMember(string nickname);
    bool AddBan(BanEntry ban, int maxBans = int.MaxValue);
    bool RemoveBan(string mask);
    bool IsBanned(string mask);
    void AddInvite(string nickname);
    bool IsInvited(string nickname);
    bool AddExcept(BanEntry except, int maxExcepts = int.MaxValue);
    bool RemoveExcept(string mask);
    bool IsExcepted(string mask);
}
