namespace MeatSpeak.Server.Permissions;

[Flags]
public enum ServerPermission : ulong
{
    None = 0,
    ManageServer      = 1UL << 0,
    ViewAuditLog      = 1UL << 1,
    ManageChannels    = 1UL << 2,
    ManageRoles       = 1UL << 3,
    ViewUserInfo      = 1UL << 4,
    KillUsers         = 1UL << 5,
    ManageBans        = 1UL << 6,
    ManageNicknames   = 1UL << 7,
    SendGlobalNotice  = 1UL << 8,
    BypassThrottle    = 1UL << 9,
    BypassSlowMode    = 1UL << 10,
    GlobalVoiceMute   = 1UL << 11,
    MoveVoiceUsers    = 1UL << 12,
    Owner             = 1UL << 63,

    All = ManageServer | ViewAuditLog | ManageChannels | ManageRoles | ViewUserInfo |
          KillUsers | ManageBans | ManageNicknames | SendGlobalNotice | BypassThrottle |
          BypassSlowMode | GlobalVoiceMute | MoveVoiceUsers,
}
