namespace MeatSpeak.Protocol;

public enum VoicePacketType : byte
{
    Audio = 0x01,
    Keepalive = 0x02,
    MediaHeader = 0x03,
}
