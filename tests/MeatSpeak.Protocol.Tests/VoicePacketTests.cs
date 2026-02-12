using System.Buffers.Binary;
using Xunit;

namespace MeatSpeak.Protocol.Tests;

public class VoicePacketTests
{
    [Fact]
    public void TryParse_ValidPacketWithPayload_Succeeds()
    {
        var data = new byte[17]; // 13 header + 4 payload
        data[0] = VoicePacket.CurrentVersion; // version
        data[1] = (byte)VoicePacketType.Audio; // type
        data[2] = (byte)VoicePacketFlags.E2E; // flags
        BinaryPrimitives.WriteUInt32BigEndian(data.AsSpan(3), 12345); // ssrc
        BinaryPrimitives.WriteUInt16BigEndian(data.AsSpan(7), 42); // sequence
        BinaryPrimitives.WriteUInt32BigEndian(data.AsSpan(9), 1000); // timestamp
        data[13] = 0xDE;
        data[14] = 0xAD;
        data[15] = 0xBE;
        data[16] = 0xEF;

        var result = VoicePacket.TryParse(data, out var packet);

        Assert.True(result);
        Assert.Equal(VoicePacket.CurrentVersion, packet.Version);
        Assert.Equal(VoicePacketType.Audio, packet.Type);
        Assert.Equal(VoicePacketFlags.E2E, packet.Flags);
        Assert.Equal(12345u, packet.Ssrc);
        Assert.Equal((ushort)42, packet.Sequence);
        Assert.Equal(1000u, packet.Timestamp);
        Assert.Equal(4, packet.Payload.Length);
        Assert.Equal(0xDE, packet.Payload[0]);
        Assert.Equal(0xEF, packet.Payload[3]);
    }

    [Fact]
    public void TryParse_ExactlyHeaderSizeNoPayload_Succeeds()
    {
        var data = new byte[VoicePacket.HeaderSize];
        data[0] = VoicePacket.CurrentVersion;
        data[1] = (byte)VoicePacketType.Keepalive;
        data[2] = (byte)VoicePacketFlags.None;

        var result = VoicePacket.TryParse(data, out var packet);

        Assert.True(result);
        Assert.Equal(VoicePacketType.Keepalive, packet.Type);
        Assert.Empty(packet.Payload.ToArray());
    }

    [Fact]
    public void TryParse_TooShortData_ReturnsFalse()
    {
        var data = new byte[VoicePacket.HeaderSize - 1]; // 12 bytes, need 13
        data[0] = VoicePacket.CurrentVersion;

        var result = VoicePacket.TryParse(data, out _);

        Assert.False(result);
    }

    [Fact]
    public void TryParse_EmptyData_ReturnsFalse()
    {
        var result = VoicePacket.TryParse(ReadOnlySpan<byte>.Empty, out _);

        Assert.False(result);
    }

    [Fact]
    public void TryParse_WrongVersion_ReturnsFalse()
    {
        var data = new byte[VoicePacket.HeaderSize];
        data[0] = 99; // wrong version

        var result = VoicePacket.TryParse(data, out _);

        Assert.False(result);
    }

    [Fact]
    public void TryParse_VersionZero_ReturnsFalse()
    {
        var data = new byte[VoicePacket.HeaderSize];
        data[0] = 0; // version 0 is not CurrentVersion (1)

        var result = VoicePacket.TryParse(data, out _);

        Assert.False(result);
    }

    [Fact]
    public void WriteThenParse_Roundtrip()
    {
        var payload = new byte[] { 0x01, 0x02, 0x03, 0x04, 0x05 };
        var buffer = new byte[VoicePacket.HeaderSize + payload.Length];

        int written = VoicePacket.Write(
            buffer,
            VoicePacketType.Audio,
            VoicePacketFlags.E2E | VoicePacketFlags.Priority,
            54321,
            100,
            99999,
            payload);

        Assert.Equal(VoicePacket.HeaderSize + payload.Length, written);

        var result = VoicePacket.TryParse(buffer, out var packet);

        Assert.True(result);
        Assert.Equal(VoicePacket.CurrentVersion, packet.Version);
        Assert.Equal(VoicePacketType.Audio, packet.Type);
        Assert.Equal(VoicePacketFlags.E2E | VoicePacketFlags.Priority, packet.Flags);
        Assert.Equal(54321u, packet.Ssrc);
        Assert.Equal((ushort)100, packet.Sequence);
        Assert.Equal(99999u, packet.Timestamp);
        Assert.Equal(payload.Length, packet.Payload.Length);
        Assert.True(packet.Payload.SequenceEqual(payload));
    }

    [Fact]
    public void Write_BufferTooSmall_ReturnsNegativeOne()
    {
        var payload = new byte[10];
        var buffer = new byte[VoicePacket.HeaderSize + payload.Length - 1]; // one byte too small

        int written = VoicePacket.Write(buffer, VoicePacketType.Audio, VoicePacketFlags.None, 1, 1, 1, payload);

        Assert.Equal(-1, written);
    }

    [Fact]
    public void HasE2E_WhenFlagSet_ReturnsTrue()
    {
        var data = new byte[VoicePacket.HeaderSize];
        data[0] = VoicePacket.CurrentVersion;
        data[2] = (byte)VoicePacketFlags.E2E;

        VoicePacket.TryParse(data, out var packet);

        Assert.True(packet.HasE2E);
        Assert.False(packet.HasSpatial);
        Assert.False(packet.HasPriority);
    }

    [Fact]
    public void HasSpatial_WhenFlagSet_ReturnsTrue()
    {
        var data = new byte[VoicePacket.HeaderSize];
        data[0] = VoicePacket.CurrentVersion;
        data[2] = (byte)VoicePacketFlags.Spatial;

        VoicePacket.TryParse(data, out var packet);

        Assert.False(packet.HasE2E);
        Assert.True(packet.HasSpatial);
        Assert.False(packet.HasPriority);
    }

    [Fact]
    public void HasPriority_WhenFlagSet_ReturnsTrue()
    {
        var data = new byte[VoicePacket.HeaderSize];
        data[0] = VoicePacket.CurrentVersion;
        data[2] = (byte)VoicePacketFlags.Priority;

        VoicePacket.TryParse(data, out var packet);

        Assert.False(packet.HasE2E);
        Assert.False(packet.HasSpatial);
        Assert.True(packet.HasPriority);
    }

    [Fact]
    public void AllFlags_WhenAllSet_AllReturnTrue()
    {
        var data = new byte[VoicePacket.HeaderSize];
        data[0] = VoicePacket.CurrentVersion;
        data[2] = (byte)(VoicePacketFlags.E2E | VoicePacketFlags.Spatial | VoicePacketFlags.Priority);

        VoicePacket.TryParse(data, out var packet);

        Assert.True(packet.HasE2E);
        Assert.True(packet.HasSpatial);
        Assert.True(packet.HasPriority);
    }

    [Fact]
    public void NoFlags_AllReturnFalse()
    {
        var data = new byte[VoicePacket.HeaderSize];
        data[0] = VoicePacket.CurrentVersion;
        data[2] = (byte)VoicePacketFlags.None;

        VoicePacket.TryParse(data, out var packet);

        Assert.False(packet.HasE2E);
        Assert.False(packet.HasSpatial);
        Assert.False(packet.HasPriority);
    }

    // --- Edge case tests ---

    [Theory]
    [InlineData(VoicePacketType.Audio, 0x01)]
    [InlineData(VoicePacketType.Keepalive, 0x02)]
    [InlineData(VoicePacketType.MediaHeader, 0x03)]
    public void TryParse_AllPacketTypes_ParseCorrectly(VoicePacketType type, byte expectedByte)
    {
        var data = new byte[VoicePacket.HeaderSize];
        data[0] = VoicePacket.CurrentVersion;
        data[1] = expectedByte;

        var result = VoicePacket.TryParse(data, out var packet);

        Assert.True(result);
        Assert.Equal(type, packet.Type);
    }

    [Fact]
    public void TryParse_UnknownPacketType_StillParses()
    {
        var data = new byte[VoicePacket.HeaderSize];
        data[0] = VoicePacket.CurrentVersion;
        data[1] = 0xFF; // unknown type

        var result = VoicePacket.TryParse(data, out var packet);

        Assert.True(result);
        Assert.Equal((VoicePacketType)0xFF, packet.Type);
    }

    [Fact]
    public void TryParse_MaxSsrc_ParsedCorrectly()
    {
        var data = new byte[VoicePacket.HeaderSize];
        data[0] = VoicePacket.CurrentVersion;
        BinaryPrimitives.WriteUInt32BigEndian(data.AsSpan(3), uint.MaxValue);

        var result = VoicePacket.TryParse(data, out var packet);

        Assert.True(result);
        Assert.Equal(uint.MaxValue, packet.Ssrc);
    }

    [Fact]
    public void TryParse_MaxSequence_ParsedCorrectly()
    {
        var data = new byte[VoicePacket.HeaderSize];
        data[0] = VoicePacket.CurrentVersion;
        BinaryPrimitives.WriteUInt16BigEndian(data.AsSpan(7), ushort.MaxValue);

        var result = VoicePacket.TryParse(data, out var packet);

        Assert.True(result);
        Assert.Equal(ushort.MaxValue, packet.Sequence);
    }

    [Fact]
    public void TryParse_MaxTimestamp_ParsedCorrectly()
    {
        var data = new byte[VoicePacket.HeaderSize];
        data[0] = VoicePacket.CurrentVersion;
        BinaryPrimitives.WriteUInt32BigEndian(data.AsSpan(9), uint.MaxValue);

        var result = VoicePacket.TryParse(data, out var packet);

        Assert.True(result);
        Assert.Equal(uint.MaxValue, packet.Timestamp);
    }

    [Fact]
    public void TryParse_LargePayload_ParsedCorrectly()
    {
        var data = new byte[VoicePacket.HeaderSize + 1500];
        data[0] = VoicePacket.CurrentVersion;
        data[1] = (byte)VoicePacketType.Audio;
        // Fill payload with pattern
        for (int i = VoicePacket.HeaderSize; i < data.Length; i++)
            data[i] = (byte)(i & 0xFF);

        var result = VoicePacket.TryParse(data, out var packet);

        Assert.True(result);
        Assert.Equal(1500, packet.Payload.Length);
        Assert.Equal((byte)(VoicePacket.HeaderSize & 0xFF), packet.Payload[0]);
    }

    [Fact]
    public void Write_EmptyPayload_WritesHeaderOnly()
    {
        var buffer = new byte[VoicePacket.HeaderSize];

        int written = VoicePacket.Write(buffer, VoicePacketType.Keepalive, VoicePacketFlags.None, 1, 1, 1, ReadOnlySpan<byte>.Empty);

        Assert.Equal(VoicePacket.HeaderSize, written);
    }

    [Fact]
    public void Write_ExactFitBuffer_Succeeds()
    {
        var payload = new byte[] { 0xAA, 0xBB };
        var buffer = new byte[VoicePacket.HeaderSize + payload.Length]; // exact fit

        int written = VoicePacket.Write(buffer, VoicePacketType.Audio, VoicePacketFlags.None, 1, 1, 1, payload);

        Assert.Equal(VoicePacket.HeaderSize + payload.Length, written);
    }

    [Theory]
    [InlineData(VoicePacketType.Audio, 0x01)]
    [InlineData(VoicePacketType.Keepalive, 0x02)]
    [InlineData(VoicePacketType.MediaHeader, 0x03)]
    public void Write_AllPacketTypes_WriteCorrectTypeByte(VoicePacketType type, byte expectedByte)
    {
        var buffer = new byte[VoicePacket.HeaderSize];

        int written = VoicePacket.Write(buffer, type, VoicePacketFlags.None, 0, 0, 0, ReadOnlySpan<byte>.Empty);

        Assert.Equal(VoicePacket.HeaderSize, written);
        Assert.Equal(expectedByte, buffer[1]);
    }

    [Fact]
    public void WriteThenParse_SequenceWraparound_Roundtrips()
    {
        var buffer = new byte[VoicePacket.HeaderSize];

        VoicePacket.Write(buffer, VoicePacketType.Audio, VoicePacketFlags.None, 1, ushort.MaxValue, 1, ReadOnlySpan<byte>.Empty);

        var result = VoicePacket.TryParse(buffer, out var packet);

        Assert.True(result);
        Assert.Equal(ushort.MaxValue, packet.Sequence);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(7)]
    public void WriteThenParse_AllFlagCombinations_Roundtrip(byte flagByte)
    {
        var flags = (VoicePacketFlags)flagByte;
        var buffer = new byte[VoicePacket.HeaderSize];

        VoicePacket.Write(buffer, VoicePacketType.Audio, flags, 42, 100, 200, ReadOnlySpan<byte>.Empty);

        var result = VoicePacket.TryParse(buffer, out var packet);

        Assert.True(result);
        Assert.Equal(flags, packet.Flags);
        Assert.Equal(42u, packet.Ssrc);
        Assert.Equal((ushort)100, packet.Sequence);
        Assert.Equal(200u, packet.Timestamp);
    }
}
