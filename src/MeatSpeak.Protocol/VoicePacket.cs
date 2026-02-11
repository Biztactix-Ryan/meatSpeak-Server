namespace MeatSpeak.Protocol;

/// <summary>
/// Parses the 13-byte voice packet header.
/// Layout: version(1) | type(1) | flags(1) | ssrc(4) | sequence(2) | timestamp(4) | payload
/// </summary>
public ref struct VoicePacket
{
    public const int HeaderSize = 13;
    public const byte CurrentVersion = 1;
    
    /// <summary>
    /// Maximum payload size for UDP packets (65000 bytes).
    /// This is slightly less than the 65507 UDP theoretical limit to account for IP/UDP headers.
    /// </summary>
    public const int MaxPayloadSize = 65000;

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

        packet.Type = (VoicePacketType)data[1];
        packet.Flags = (VoicePacketFlags)data[2];
        packet.Ssrc = System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(data[3..]);
        packet.Sequence = System.Buffers.Binary.BinaryPrimitives.ReadUInt16BigEndian(data[7..]);
        packet.Timestamp = System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(data[9..]);
        packet.Payload = data[HeaderSize..];

        return true;
    }

    public static int Write(Span<byte> buffer, VoicePacketType type, VoicePacketFlags flags, uint ssrc, ushort sequence, uint timestamp, ReadOnlySpan<byte> payload)
    {
        // Validate payload size doesn't exceed UDP packet limits
        if (payload.Length > MaxPayloadSize)
            return -1;

        // Check for integer overflow when adding HeaderSize + payload.Length
        // This is defense-in-depth: while MaxPayloadSize prevents realistic overflow,
        // this check ensures safety even if MaxPayloadSize were changed or if the
        // method were called with a slice of a larger structure
        if (payload.Length > int.MaxValue - HeaderSize)
            return -1;

        int totalSize = HeaderSize + payload.Length;
        
        if (buffer.Length < totalSize)
            return -1;

        buffer[0] = CurrentVersion;
        buffer[1] = (byte)type;
        buffer[2] = (byte)flags;
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32BigEndian(buffer[3..], ssrc);
        System.Buffers.Binary.BinaryPrimitives.WriteUInt16BigEndian(buffer[7..], sequence);
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32BigEndian(buffer[9..], timestamp);
        payload.CopyTo(buffer[HeaderSize..]);

        return totalSize;
    }
}
