namespace MeatSpeak.Server.Core.Modes;

public enum ModeType
{
    A,  // List modes (e.g., ban list +b) - always has parameter
    B,  // Param required on set and unset (e.g., +k channel key)
    C,  // Param required on set only (e.g., +l user limit)
    D,  // No parameter (e.g., +i invite-only)
}
