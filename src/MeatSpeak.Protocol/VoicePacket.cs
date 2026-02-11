namespace MeatSpeak.Protocol;

/// <summary>
/// Parses the 13-byte voice packet header.
/// Layout: version(1) | type(1) | flags(1) | ssrc(4) | sequence(2) | timestamp(4) | payload
/// </summary>
public ref struct VoicePacket
{
    public const int HeaderSize = 13;
    public const byte CurrentVersion = 1;

    public byte Version;
    public VoicePacketType Type;
    public VoicePacketFlags Flags;
    public uint Ssrc;
    public ushort Sequence;
    public uint Timestamp;
    public ReadOnlySpan<byte> Payload;

    public bool HasE2E => (Flags & VoicePacketFlags.E2E) != 0;
    public bool HasSpatial => (Flags & VoicePacketFlags.Spatial) != 0;
    public bool HasPriority => (Flags & VoicePacketFlags.Priority) != 0;

    public static bool TryParse(ReadOnlySpan<byte> data, out VoicePacket packet)
    {
        packet = default;
        if (data.Length < HeaderSize)
            return false;

        packet.Version = data[0];
        if (packet.Version != CurrentVersion)
            return false;

        // Validate VoicePacketType enum - use range check for performance
        var typeValue = data[1];
        if (typeValue < 1 || typeValue > 3)
            return false;

        // Validate VoicePacketFlags - check that only defined flag bits are set
        var flagsValue = data[2];
        const byte ValidFlagsMask = (byte)(VoicePacketFlags.E2E | VoicePacketFlags.Spatial | VoicePacketFlags.Priority);
        if ((flagsValue & ~ValidFlagsMask) != 0)
            return false;

        packet.Type = (VoicePacketType)typeValue;
        packet.Flags = (VoicePacketFlags)flagsValue;
        packet.Ssrc = System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(data[3..]);
        packet.Sequence = System.Buffers.Binary.BinaryPrimitives.ReadUInt16BigEndian(data[7..]);
        packet.Timestamp = System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(data[9..]);
        packet.Payload = data[HeaderSize..];

        return true;
    }

    public static int Write(Span<byte> buffer, VoicePacketType type, VoicePacketFlags flags, uint ssrc, ushort sequence, uint timestamp, ReadOnlySpan<byte> payload)
    {
        if (buffer.Length < HeaderSize + payload.Length)
            return -1;

        buffer[0] = CurrentVersion;
        buffer[1] = (byte)type;
        buffer[2] = (byte)flags;
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32BigEndian(buffer[3..], ssrc);
        System.Buffers.Binary.BinaryPrimitives.WriteUInt16BigEndian(buffer[7..], sequence);
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32BigEndian(buffer[9..], timestamp);
        payload.CopyTo(buffer[HeaderSize..]);

        return HeaderSize + payload.Length;
    }
}
