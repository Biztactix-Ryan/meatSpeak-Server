namespace MeatSpeak.Protocol;

[Flags]
public enum VoicePacketFlags : byte
{
    None = 0,
    E2E = 1 << 0,       // Payload encrypted with group key
    Spatial = 1 << 1,    // 12 bytes of position data appended
    Priority = 1 << 2,   // Sender is priority speaker
}
