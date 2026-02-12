using Xunit;
using NSubstitute;
using MeatSpeak.Server.Capabilities;
using MeatSpeak.Server.Core.Sessions;

namespace MeatSpeak.Server.Tests.Capabilities;

public class BatchHelperTests
{
    private ISession CreateSession(bool batchCap = true, bool serverTime = false)
    {
        var session = Substitute.For<ISession>();
        var info = new SessionInfo();
        if (batchCap)
            info.CapState.Acknowledged.Add("batch");
        if (serverTime)
            info.CapState.Acknowledged.Add("server-time");
        session.Info.Returns(info);
        session.Id.Returns("test-id");
        return session;
    }

    [Fact]
    public void GenerateReference_ReturnsUniqueReferences()
    {
        var ref1 = BatchHelper.GenerateReference();
        var ref2 = BatchHelper.GenerateReference();

        Assert.NotEqual(ref1, ref2);
    }

    [Fact]
    public void GenerateReference_ReturnsHexString()
    {
        var reference = BatchHelper.GenerateReference();

        Assert.Equal(8, reference.Length);
        Assert.True(reference.All(c => "0123456789abcdef".Contains(c)));
    }

    [Fact]
    public async Task StartBatch_WithBatchCap_SendsBatchMessage()
    {
        var session = CreateSession(batchCap: true);

        await BatchHelper.StartBatch(session, "abc123", "chathistory", "#channel");

        await session.Received().SendMessageAsync(
            Arg.Any<string>(), "BATCH",
            Arg.Is<string[]>(p => p[0] == "+abc123" && p[1] == "chathistory" && p[2] == "#channel"));
    }

    [Fact]
    public async Task StartBatch_WithoutBatchCap_DoesNotSend()
    {
        var session = CreateSession(batchCap: false);

        await BatchHelper.StartBatch(session, "abc123", "chathistory");

        await session.DidNotReceive().SendMessageAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string[]>());
        await session.DidNotReceive().SendTaggedMessageAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string[]>());
    }

    [Fact]
    public async Task EndBatch_WithBatchCap_SendsEndBatchMessage()
    {
        var session = CreateSession(batchCap: true);

        await BatchHelper.EndBatch(session, "abc123");

        await session.Received().SendMessageAsync(
            Arg.Any<string>(), "BATCH",
            Arg.Is<string[]>(p => p[0] == "-abc123"));
    }

    [Fact]
    public async Task EndBatch_WithoutBatchCap_DoesNotSend()
    {
        var session = CreateSession(batchCap: false);

        await BatchHelper.EndBatch(session, "abc123");

        await session.DidNotReceive().SendMessageAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string[]>());
    }

    [Fact]
    public async Task StartBatch_WithServerTime_SendsTaggedMessage()
    {
        var session = CreateSession(batchCap: true, serverTime: true);

        await BatchHelper.StartBatch(session, "ref1", "netsplit");

        await session.Received().SendTaggedMessageAsync(
            Arg.Is<string>(t => t.StartsWith("time=")),
            Arg.Any<string>(), "BATCH",
            Arg.Is<string[]>(p => p[0] == "+ref1" && p[1] == "netsplit"));
    }
}
