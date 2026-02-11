using MeatSpeak.Protocol;
using MeatSpeak.Server.Core.Commands;
using MeatSpeak.Server.Core.Sessions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace MeatSpeak.Server.Core.Tests;

public class CommandRegistryTests
{
    private sealed class FakeHandler : ICommandHandler
    {
        public string Command { get; }
        public SessionState MinimumState => SessionState.Registered;

        public FakeHandler(string command)
        {
            Command = command;
        }

        public ValueTask HandleAsync(ISession session, IrcMessage message, CancellationToken ct = default)
        {
            return ValueTask.CompletedTask;
        }
    }

    private CommandRegistry CreateRegistry() =>
        new(NullLogger<CommandRegistry>.Instance);

    [Fact]
    public void Register_And_Resolve_ReturnsHandler()
    {
        var registry = CreateRegistry();
        var handler = new FakeHandler("PRIVMSG");

        registry.Register(handler);

        var resolved = registry.Resolve("PRIVMSG");
        Assert.NotNull(resolved);
        Assert.Same(handler, resolved);
    }

    [Fact]
    public void Resolve_UnknownCommand_ReturnsNull()
    {
        var registry = CreateRegistry();

        var resolved = registry.Resolve("NONEXISTENT");
        Assert.Null(resolved);
    }

    [Fact]
    public void DuplicateRegistration_KeepsFirst()
    {
        var registry = CreateRegistry();
        var first = new FakeHandler("JOIN");
        var second = new FakeHandler("JOIN");

        registry.Register(first);
        registry.Register(second);

        var resolved = registry.Resolve("JOIN");
        Assert.Same(first, resolved);
    }

    [Fact]
    public void All_ReturnsAllRegisteredHandlers()
    {
        var registry = CreateRegistry();
        var privmsg = new FakeHandler("PRIVMSG");
        var join = new FakeHandler("JOIN");
        var part = new FakeHandler("PART");

        registry.Register(privmsg);
        registry.Register(join);
        registry.Register(part);

        var all = registry.All;
        Assert.Equal(3, all.Count);
        Assert.Same(privmsg, all["PRIVMSG"]);
        Assert.Same(join, all["JOIN"]);
        Assert.Same(part, all["PART"]);
    }
}
