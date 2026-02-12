using Xunit;
using NSubstitute;
using MeatSpeak.Server.Core.Commands;
using MeatSpeak.Server.Core.Modes;
using MeatSpeak.Server.Core.Capabilities;
using MeatSpeak.Server.Core.Events;
using MeatSpeak.Server.Core.Server;
using MeatSpeak.Server.Core.Sessions;
using MeatSpeak.Server.State;
using Microsoft.Extensions.Logging.Abstractions;

namespace MeatSpeak.Server.Tests;

public class ServerStateTests
{
    private readonly ServerState _state;

    public ServerStateTests()
    {
        var config = new ServerConfig { ServerName = "test.server" };
        var commands = new CommandRegistry(NullLogger<CommandRegistry>.Instance);
        var modes = new ModeRegistry();
        var caps = new CapabilityRegistry();
        var events = Substitute.For<IEventBus>();
        _state = new ServerState(config, commands, modes, caps, events);
    }

    private ISession CreateMockSession(string id, string? nickname = null)
    {
        var session = Substitute.For<ISession>();
        session.Id.Returns(id);
        var info = new SessionInfo { Nickname = nickname };
        session.Info.Returns(info);
        return session;
    }

    // --- Session management ---

    [Fact]
    public void AddSession_StoresSession_And_IncrementsConnectionCount()
    {
        var session = CreateMockSession("s1");

        _state.AddSession(session);

        Assert.Equal(1, _state.ConnectionCount);
        Assert.True(_state.Sessions.ContainsKey("s1"));
        Assert.Same(session, _state.Sessions["s1"]);
    }

    [Fact]
    public void RemoveSession_RemovesSession_And_DecrementsConnectionCount()
    {
        var session = CreateMockSession("s1");
        _state.AddSession(session);

        _state.RemoveSession("s1");

        Assert.Equal(0, _state.ConnectionCount);
        Assert.False(_state.Sessions.ContainsKey("s1"));
    }

    [Fact]
    public void RemoveSession_CleansUpNickIndex()
    {
        var session = CreateMockSession("s1", "Alice");
        _state.AddSession(session);
        _state.UpdateNickIndex(null, "Alice", session);

        // Verify nick is indexed before removal
        Assert.Same(session, _state.FindSessionByNick("Alice"));

        _state.RemoveSession("s1");

        // Nick index should be cleaned up
        Assert.Null(_state.FindSessionByNick("Alice"));
    }

    [Fact]
    public void RemoveSession_UnknownId_IsNoOp()
    {
        var session = CreateMockSession("s1");
        _state.AddSession(session);

        _state.RemoveSession("unknown-id");

        // Original session is unaffected
        Assert.Equal(1, _state.ConnectionCount);
        Assert.True(_state.Sessions.ContainsKey("s1"));
    }

    // --- Nick index ---

    [Fact]
    public void FindSessionByNick_UnknownNick_ReturnsNull()
    {
        Assert.Null(_state.FindSessionByNick("NonExistent"));
    }

    [Fact]
    public void FindSessionByNick_ReturnsSession_AfterUpdateNickIndex()
    {
        var session = CreateMockSession("s1", "Bob");
        _state.AddSession(session);
        _state.UpdateNickIndex(null, "Bob", session);

        var found = _state.FindSessionByNick("Bob");

        Assert.NotNull(found);
        Assert.Same(session, found);
    }

    [Fact]
    public void UpdateNickIndex_RemovesOldNick_And_AddsNewNick()
    {
        var session = CreateMockSession("s1", "OldNick");
        _state.AddSession(session);
        _state.UpdateNickIndex(null, "OldNick", session);

        // Change nick
        _state.UpdateNickIndex("OldNick", "NewNick", session);

        Assert.Null(_state.FindSessionByNick("OldNick"));
        Assert.Same(session, _state.FindSessionByNick("NewNick"));
    }

    [Fact]
    public void UpdateNickIndex_NullOldNick_OnlyAdds()
    {
        var session = CreateMockSession("s1", "Fresh");
        _state.AddSession(session);

        _state.UpdateNickIndex(null, "Fresh", session);

        Assert.Same(session, _state.FindSessionByNick("Fresh"));
    }

    [Fact]
    public void UpdateNickIndex_NullNewNick_OnlyRemoves()
    {
        var session = CreateMockSession("s1", "GoingAway");
        _state.AddSession(session);
        _state.UpdateNickIndex(null, "GoingAway", session);

        // Remove the nick without adding a new one
        _state.UpdateNickIndex("GoingAway", null, session);

        Assert.Null(_state.FindSessionByNick("GoingAway"));
    }

    [Fact]
    public void FindSessionByNick_IsCaseInsensitive()
    {
        var session = CreateMockSession("s1", "Alice");
        _state.AddSession(session);
        _state.UpdateNickIndex(null, "Alice", session);

        Assert.Same(session, _state.FindSessionByNick("alice"));
        Assert.Same(session, _state.FindSessionByNick("ALICE"));
        Assert.Same(session, _state.FindSessionByNick("aLiCe"));
    }

    // --- Channel management ---

    [Fact]
    public void GetOrCreateChannel_CreatesNewChannel()
    {
        var channel = _state.GetOrCreateChannel("#test");

        Assert.NotNull(channel);
        Assert.Equal("#test", channel.Name);
        Assert.Equal(1, _state.ChannelCount);
    }

    [Fact]
    public void GetOrCreateChannel_ReturnsExistingChannel()
    {
        var first = _state.GetOrCreateChannel("#test");
        var second = _state.GetOrCreateChannel("#test");

        Assert.Same(first, second);
        Assert.Equal(1, _state.ChannelCount);
    }

    [Fact]
    public void RemoveChannel_RemovesChannel()
    {
        _state.GetOrCreateChannel("#test");
        Assert.Equal(1, _state.ChannelCount);

        _state.RemoveChannel("#test");

        Assert.Equal(0, _state.ChannelCount);
        Assert.False(_state.Channels.ContainsKey("#test"));
    }

    [Fact]
    public void RemoveChannel_UnknownChannel_IsNoOp()
    {
        _state.GetOrCreateChannel("#existing");

        _state.RemoveChannel("#nonexistent");

        Assert.Equal(1, _state.ChannelCount);
    }

    [Fact]
    public void ChannelCount_ReflectsChannelCount()
    {
        Assert.Equal(0, _state.ChannelCount);

        _state.GetOrCreateChannel("#one");
        Assert.Equal(1, _state.ChannelCount);

        _state.GetOrCreateChannel("#two");
        Assert.Equal(2, _state.ChannelCount);

        _state.GetOrCreateChannel("#three");
        Assert.Equal(3, _state.ChannelCount);

        _state.RemoveChannel("#two");
        Assert.Equal(2, _state.ChannelCount);
    }

    [Fact]
    public void Channels_Dictionary_IsCaseInsensitive()
    {
        var channel = _state.GetOrCreateChannel("#Test");

        // Lookup via Channels dictionary with different casing
        Assert.True(_state.Channels.ContainsKey("#test"));
        Assert.True(_state.Channels.ContainsKey("#TEST"));
        Assert.Same(channel, _state.Channels["#test"]);

        // GetOrCreateChannel with different case returns the same channel
        var same = _state.GetOrCreateChannel("#TEST");
        Assert.Same(channel, same);
        Assert.Equal(1, _state.ChannelCount);
    }

    // --- WHOWAS ---

    [Fact]
    public void RecordWhowas_StoresEntry()
    {
        var entry = new WhowasEntry("Alice", "alice", "host.example.com", "Alice Smith", DateTimeOffset.UtcNow);

        _state.RecordWhowas(entry);

        var results = _state.GetWhowas("Alice");
        Assert.Single(results);
        Assert.Equal("Alice", results[0].Nickname);
        Assert.Equal("alice", results[0].Username);
        Assert.Equal("host.example.com", results[0].Hostname);
        Assert.Equal("Alice Smith", results[0].Realname);
    }

    [Fact]
    public void GetWhowas_ReturnsEntries_NewestFirst()
    {
        var older = new WhowasEntry("Alice", "alice_old", "host1", "Old Alice", DateTimeOffset.UtcNow.AddMinutes(-10));
        var newer = new WhowasEntry("Alice", "alice_new", "host2", "New Alice", DateTimeOffset.UtcNow);

        _state.RecordWhowas(older);
        _state.RecordWhowas(newer);

        var results = _state.GetWhowas("Alice");
        Assert.Equal(2, results.Count);
        Assert.Equal("alice_new", results[0].Username);
        Assert.Equal("alice_old", results[1].Username);
    }

    [Fact]
    public void GetWhowas_UnknownNick_ReturnsEmptyList()
    {
        var results = _state.GetWhowas("Nobody");

        Assert.Empty(results);
    }

    [Fact]
    public void GetWhowas_RespectsMaxCount()
    {
        for (int i = 0; i < 10; i++)
            _state.RecordWhowas(new WhowasEntry("Alice", $"user{i}", "host", "Alice", DateTimeOffset.UtcNow));

        var results = _state.GetWhowas("Alice", 3);

        Assert.Equal(3, results.Count);
    }

    [Fact]
    public void RecordWhowas_CapsAtMaxWhowasEntries()
    {
        for (int i = 0; i < 110; i++)
            _state.RecordWhowas(new WhowasEntry("Alice", $"user{i}", "host", "Alice", DateTimeOffset.UtcNow));

        // Request more than the cap to verify it was enforced
        var results = _state.GetWhowas("Alice", 200);

        Assert.Equal(100, results.Count);
    }
}
