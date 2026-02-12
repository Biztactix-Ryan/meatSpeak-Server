using Xunit;
using MeatSpeak.Server.Core.Commands;
using MeatSpeak.Server.Core.Modes;
using MeatSpeak.Server.Core.Capabilities;
using MeatSpeak.Server.Core.Events;
using MeatSpeak.Server.Core.Server;
using MeatSpeak.Server.Core.Sessions;
using MeatSpeak.Server.State;
using NSubstitute;
using Microsoft.Extensions.Logging.Abstractions;

namespace MeatSpeak.Server.Tests;

public class ServerStateWhowasTests
{
    private readonly ServerState _state;

    public ServerStateWhowasTests()
    {
        var config = new ServerConfig { ServerName = "test.server" };
        var commands = new CommandRegistry(NullLogger<CommandRegistry>.Instance);
        var modes = new ModeRegistry();
        var caps = new CapabilityRegistry();
        var events = Substitute.For<IEventBus>();
        _state = new ServerState(config, commands, modes, caps, events);
    }

    [Fact]
    public void RecordWhowas_StoresEntry()
    {
        var entry = new WhowasEntry("Alice", "alice", "host", "Alice Smith", DateTimeOffset.UtcNow);
        _state.RecordWhowas(entry);

        var results = _state.GetWhowas("Alice");
        Assert.Single(results);
        Assert.Equal("Alice", results[0].Nickname);
        Assert.Equal("alice", results[0].Username);
        Assert.Equal("host", results[0].Hostname);
        Assert.Equal("Alice Smith", results[0].Realname);
    }

    [Fact]
    public void GetWhowas_NonExistentNick_ReturnsEmpty()
    {
        var results = _state.GetWhowas("NonExistent");
        Assert.Empty(results);
    }

    [Fact]
    public void GetWhowas_CaseInsensitive()
    {
        var entry = new WhowasEntry("Alice", "alice", "host", "Alice", DateTimeOffset.UtcNow);
        _state.RecordWhowas(entry);

        var results = _state.GetWhowas("alice");
        Assert.Single(results);
    }

    [Fact]
    public void RecordWhowas_MultipleEntries_ReturnsNewestFirst()
    {
        var older = new WhowasEntry("Alice", "alice1", "host1", "Old Alice", DateTimeOffset.UtcNow.AddMinutes(-10));
        var newer = new WhowasEntry("Alice", "alice2", "host2", "New Alice", DateTimeOffset.UtcNow);

        _state.RecordWhowas(older);
        _state.RecordWhowas(newer);

        var results = _state.GetWhowas("Alice");
        Assert.Equal(2, results.Count);
        Assert.Equal("alice2", results[0].Username);
        Assert.Equal("alice1", results[1].Username);
    }

    [Fact]
    public void GetWhowas_RespectsMaxCount()
    {
        for (int i = 0; i < 5; i++)
            _state.RecordWhowas(new WhowasEntry("Alice", $"user{i}", "host", "Alice", DateTimeOffset.UtcNow));

        var results = _state.GetWhowas("Alice", 3);
        Assert.Equal(3, results.Count);
    }

    [Fact]
    public void RecordWhowas_BoundsHistory()
    {
        // Record more than the internal limit (100)
        for (int i = 0; i < 110; i++)
            _state.RecordWhowas(new WhowasEntry("Alice", $"user{i}", "host", "Alice", DateTimeOffset.UtcNow));

        var results = _state.GetWhowas("Alice", 200);
        Assert.Equal(100, results.Count);
    }
}
