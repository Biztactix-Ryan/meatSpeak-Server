using Xunit;

namespace MeatSpeak.Server.Voice.Tests;

public class SilenceDetectorTests
{
    [Fact]
    public void EmptyPayload_IsSilence()
    {
        var detector = new SilenceDetector();

        Assert.True(detector.IsSilence(ReadOnlySpan<byte>.Empty));
    }

    [Fact]
    public void OneBytePayload_IsSilence()
    {
        var detector = new SilenceDetector();

        Assert.True(detector.IsSilence(new byte[] { 0x01 }));
    }

    [Fact]
    public void TwoBytePayload_IsSilence()
    {
        var detector = new SilenceDetector();

        Assert.True(detector.IsSilence(new byte[] { 0x01, 0x02 }));
    }

    [Fact]
    public void ThreeBytePayload_IsSilence()
    {
        var detector = new SilenceDetector();

        Assert.True(detector.IsSilence(new byte[] { 0x01, 0x02, 0x03 }));
    }

    [Fact]
    public void FourBytePayload_IsNotSilence()
    {
        var detector = new SilenceDetector();

        Assert.False(detector.IsSilence(new byte[] { 0x01, 0x02, 0x03, 0x04 }));
    }

    [Fact]
    public void LargePayload_IsNotSilence()
    {
        var detector = new SilenceDetector();

        Assert.False(detector.IsSilence(new byte[256]));
    }
}
