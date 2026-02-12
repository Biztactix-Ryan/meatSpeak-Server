using MeatSpeak.Server.Core.Commands;
using MeatSpeak.Server.Handlers.Channels;
using MeatSpeak.Server.Handlers.Connection;
using MeatSpeak.Server.Handlers.Messaging;
using Xunit;

namespace MeatSpeak.Server.Tests;

public class FloodPenaltyAttributeTests
{
    private static int? GetFloodPenaltyCost(Type handlerType)
    {
        var attrs = handlerType.GetCustomAttributes(typeof(FloodPenaltyAttribute), false);
        if (attrs.Length == 0) return null;
        return ((FloodPenaltyAttribute)attrs[0]).Cost;
    }

    // ─── Cost 0: keepalive / registration / disconnect ───

    [Fact]
    public void PingHandler_HasCostZero()
        => Assert.Equal(0, GetFloodPenaltyCost(typeof(PingHandler)));

    [Fact]
    public void PongHandler_HasCostZero()
        => Assert.Equal(0, GetFloodPenaltyCost(typeof(PongHandler)));

    [Fact]
    public void CapHandler_HasCostZero()
        => Assert.Equal(0, GetFloodPenaltyCost(typeof(CapHandler)));

    [Fact]
    public void QuitHandler_HasCostZero()
        => Assert.Equal(0, GetFloodPenaltyCost(typeof(QuitHandler)));

    // ─── Cost 2: broadcast commands ───

    [Fact]
    public void PrivmsgHandler_HasCostTwo()
        => Assert.Equal(2, GetFloodPenaltyCost(typeof(PrivmsgHandler)));

    [Fact]
    public void NoticeHandler_HasCostTwo()
        => Assert.Equal(2, GetFloodPenaltyCost(typeof(NoticeHandler)));

    [Fact]
    public void JoinHandler_HasCostTwo()
        => Assert.Equal(2, GetFloodPenaltyCost(typeof(JoinHandler)));

    [Fact]
    public void NickHandler_HasCostTwo()
        => Assert.Equal(2, GetFloodPenaltyCost(typeof(NickHandler)));

    [Fact]
    public void ModeHandler_HasCostTwo()
        => Assert.Equal(2, GetFloodPenaltyCost(typeof(ModeHandler)));

    [Fact]
    public void KickHandler_HasCostTwo()
        => Assert.Equal(2, GetFloodPenaltyCost(typeof(KickHandler)));

    [Fact]
    public void TopicHandler_HasCostTwo()
        => Assert.Equal(2, GetFloodPenaltyCost(typeof(TopicHandler)));

    [Fact]
    public void InviteHandler_HasCostTwo()
        => Assert.Equal(2, GetFloodPenaltyCost(typeof(InviteHandler)));

    // ─── Default cost 1: no attribute ───

    [Fact]
    public void PassHandler_HasNoAttribute_DefaultCostOne()
        => Assert.Null(GetFloodPenaltyCost(typeof(PassHandler)));

    [Fact]
    public void UserHandler_HasNoAttribute_DefaultCostOne()
        => Assert.Null(GetFloodPenaltyCost(typeof(UserHandler)));

    [Fact]
    public void WhoHandler_HasNoAttribute_DefaultCostOne()
        => Assert.Null(GetFloodPenaltyCost(typeof(WhoHandler)));

    [Fact]
    public void WhoisHandler_HasNoAttribute_DefaultCostOne()
        => Assert.Null(GetFloodPenaltyCost(typeof(WhoisHandler)));

    [Fact]
    public void ListHandler_HasNoAttribute_DefaultCostOne()
        => Assert.Null(GetFloodPenaltyCost(typeof(ListHandler)));

    [Fact]
    public void NamesHandler_HasNoAttribute_DefaultCostOne()
        => Assert.Null(GetFloodPenaltyCost(typeof(NamesHandler)));

    [Fact]
    public void PartHandler_HasNoAttribute_DefaultCostOne()
        => Assert.Null(GetFloodPenaltyCost(typeof(PartHandler)));

    [Fact]
    public void AwayHandler_HasNoAttribute_DefaultCostOne()
        => Assert.Null(GetFloodPenaltyCost(typeof(AwayHandler)));

    // ─── Attribute semantics ───

    [Fact]
    public void FloodPenaltyAttribute_StoresCost()
    {
        var attr = new FloodPenaltyAttribute(3);
        Assert.Equal(3, attr.Cost);
    }

    [Fact]
    public void FloodPenaltyAttribute_CostZero()
    {
        var attr = new FloodPenaltyAttribute(0);
        Assert.Equal(0, attr.Cost);
    }

    [Fact]
    public void FloodPenaltyAttribute_IsClassOnly()
    {
        var usage = (AttributeUsageAttribute)typeof(FloodPenaltyAttribute)
            .GetCustomAttributes(typeof(AttributeUsageAttribute), false)[0];
        Assert.Equal(AttributeTargets.Class, usage.ValidOn);
        Assert.False(usage.AllowMultiple);
    }
}
