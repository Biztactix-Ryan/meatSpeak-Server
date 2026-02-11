namespace MeatSpeak.Server.Permissions;

public static class BuiltInRoles
{
    public static readonly Guid EveryoneId = Guid.Empty;

    public static Role Everyone() => new(
        EveryoneId,
        "@everyone",
        0,
        ServerPermission.None,
        ChannelPermission.ViewChannel | ChannelPermission.SendMessages | ChannelPermission.EmbedLinks |
        ChannelPermission.VoiceConnect | ChannelPermission.VoiceSpeak | ChannelPermission.VoiceUseVAD
    );

    public static Role Moderator() => new(
        Guid.NewGuid(),
        "Moderator",
        10,
        ServerPermission.ManageBans | ServerPermission.ViewUserInfo | ServerPermission.ManageNicknames,
        ChannelPermission.All & ~(ChannelPermission.ManageChannel | ChannelPermission.ManageVoiceKeys)
    );

    public static Role Admin() => new(
        Guid.NewGuid(),
        "Admin",
        20,
        ServerPermission.All & ~ServerPermission.Owner,
        ChannelPermission.All
    );
}
