namespace MeatSpeak.Server.Permissions;

[Flags]
public enum ChannelPermission : ulong
{
    None = 0,
    // Text
    ViewChannel       = 1UL << 0,
    SendMessages      = 1UL << 1,
    EmbedLinks        = 1UL << 2,
    SetTopic          = 1UL << 3,
    ManageMessages    = 1UL << 4,
    // Admin
    ManageChannel     = 1UL << 8,
    ManageModes       = 1UL << 9,
    KickMembers       = 1UL << 10,
    BanMembers        = 1UL << 11,
    InviteMembers     = 1UL << 12,
    // Voice
    VoiceConnect      = 1UL << 16,
    VoiceSpeak        = 1UL << 17,
    VoiceUseVAD       = 1UL << 18,
    VoiceMuteMembers  = 1UL << 19,
    ManageVoiceKeys   = 1UL << 20,
    VoicePriority     = 1UL << 21,

    AllText = ViewChannel | SendMessages | EmbedLinks | SetTopic | ManageMessages,
    AllAdmin = ManageChannel | ManageModes | KickMembers | BanMembers | InviteMembers,
    AllVoice = VoiceConnect | VoiceSpeak | VoiceUseVAD | VoiceMuteMembers | ManageVoiceKeys | VoicePriority,
    All = AllText | AllAdmin | AllVoice,
}
