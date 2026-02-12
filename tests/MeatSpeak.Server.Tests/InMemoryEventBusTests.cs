using Xunit;
using MeatSpeak.Server.Events;
using MeatSpeak.Server.Core.Events;

namespace MeatSpeak.Server.Tests;

public class InMemoryEventBusTests
{
    private readonly InMemoryEventBus _bus = new();

    [Fact]
    public void Publish_WithNoSubscribers_DoesNotThrow()
    {
        var evt = new SessionConnectedEvent("session-1");

        var exception = Record.Exception(() => _bus.Publish(evt));

        Assert.Null(exception);
    }

    [Fact]
    public void Subscribe_AndPublish_DeliversEvent()
    {
        SessionConnectedEvent? received = null;
        _bus.Subscribe<SessionConnectedEvent>(e => received = e);

        var evt = new SessionConnectedEvent("session-1");
        _bus.Publish(evt);

        Assert.NotNull(received);
        Assert.Equal("session-1", received!.SessionId);
    }

    [Fact]
    public void MultipleSubscribers_AllReceiveEvent()
    {
        var receivedCount = 0;
        _bus.Subscribe<SessionConnectedEvent>(_ => receivedCount++);
        _bus.Subscribe<SessionConnectedEvent>(_ => receivedCount++);
        _bus.Subscribe<SessionConnectedEvent>(_ => receivedCount++);

        _bus.Publish(new SessionConnectedEvent("session-1"));

        Assert.Equal(3, receivedCount);
    }

    [Fact]
    public void Dispose_UnsubscribesHandler()
    {
        var callCount = 0;
        var subscription = _bus.Subscribe<SessionConnectedEvent>(_ => callCount++);

        _bus.Publish(new SessionConnectedEvent("session-1"));
        Assert.Equal(1, callCount);

        subscription.Dispose();

        _bus.Publish(new SessionConnectedEvent("session-2"));
        Assert.Equal(1, callCount);
    }

    [Fact]
    public void AfterUnsubscribe_HandlerNoLongerCalled()
    {
        var calls = new List<string>();
        var sub = _bus.Subscribe<SessionConnectedEvent>(e => calls.Add(e.SessionId));

        _bus.Publish(new SessionConnectedEvent("first"));
        sub.Dispose();
        _bus.Publish(new SessionConnectedEvent("second"));
        _bus.Publish(new SessionConnectedEvent("third"));

        Assert.Single(calls);
        Assert.Equal("first", calls[0]);
    }

    [Fact]
    public void DifferentEventTypes_HaveSeparateSubscriberLists()
    {
        SessionConnectedEvent? connectedReceived = null;
        SessionDisconnectedEvent? disconnectedReceived = null;

        _bus.Subscribe<SessionConnectedEvent>(e => connectedReceived = e);
        _bus.Subscribe<SessionDisconnectedEvent>(e => disconnectedReceived = e);

        _bus.Publish(new SessionConnectedEvent("session-1"));

        Assert.NotNull(connectedReceived);
        Assert.Null(disconnectedReceived);

        _bus.Publish(new SessionDisconnectedEvent("session-2", "quit"));

        Assert.NotNull(disconnectedReceived);
        Assert.Equal("session-2", disconnectedReceived!.SessionId);
    }

    [Fact]
    public void PublishOneType_DoesNotTriggerOtherTypeSubscribers()
    {
        var nickChangedCount = 0;
        var connectedCount = 0;

        _bus.Subscribe<NickChangedEvent>(_ => nickChangedCount++);
        _bus.Subscribe<SessionConnectedEvent>(_ => connectedCount++);

        _bus.Publish(new SessionConnectedEvent("session-1"));

        Assert.Equal(1, connectedCount);
        Assert.Equal(0, nickChangedCount);
    }
}
