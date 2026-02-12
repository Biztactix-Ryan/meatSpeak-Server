using Xunit;

namespace MeatSpeak.Server.Voice.Tests;

public class SsrcManagerTests
{
    // ─── Allocation ───

    [Fact]
    public void Allocate_ReturnsIncrementingSsrcs()
    {
        var manager = new SsrcManager();

        var ssrc1 = manager.Allocate("session1");
        var ssrc2 = manager.Allocate("session2");
        var ssrc3 = manager.Allocate("session3");

        Assert.Equal(1u, ssrc1);
        Assert.Equal(2u, ssrc2);
        Assert.Equal(3u, ssrc3);
    }

    [Fact]
    public void GetSessionId_ReturnsSessionIdForAllocatedSsrc()
    {
        var manager = new SsrcManager();

        var ssrc = manager.Allocate("session1");

        Assert.Equal("session1", manager.GetSessionId(ssrc));
    }

    [Fact]
    public void GetSessionId_ReturnsNullForUnknownSsrc()
    {
        var manager = new SsrcManager();

        Assert.Null(manager.GetSessionId(999));
    }

    // ─── Release ───

    [Fact]
    public void Release_RemovesSsrcMapping()
    {
        var manager = new SsrcManager();
        var ssrc = manager.Allocate("session1");

        manager.Release(ssrc);

        Assert.Null(manager.GetSessionId(ssrc));
    }

    [Fact]
    public void AfterRelease_GetSessionIdReturnsNull()
    {
        var manager = new SsrcManager();
        var ssrc = manager.Allocate("session1");
        Assert.Equal("session1", manager.GetSessionId(ssrc));

        manager.Release(ssrc);

        Assert.Null(manager.GetSessionId(ssrc));
    }

    // ─── Multiple allocations ───

    [Fact]
    public void MultipleAllocationsForSameSession_AreDistinct()
    {
        var manager = new SsrcManager();

        var ssrc1 = manager.Allocate("session1");
        var ssrc2 = manager.Allocate("session1");

        Assert.NotEqual(ssrc1, ssrc2);
        Assert.Equal("session1", manager.GetSessionId(ssrc1));
        Assert.Equal("session1", manager.GetSessionId(ssrc2));
    }
}
