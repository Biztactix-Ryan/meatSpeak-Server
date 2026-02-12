using Xunit;
using MeatSpeak.Server.Diagnostics;

namespace MeatSpeak.Server.Tests;

public class ServerMetricsTests
{
    private readonly ServerMetrics _metrics = new();

    [Fact]
    public void InitialCounters_AreZero()
    {
        Assert.Equal(0, _metrics.ConnectionsAccepted);
        Assert.Equal(0, _metrics.ConnectionsActive);
        Assert.Equal(0, _metrics.RegistrationsCompleted);
        Assert.Equal(0, _metrics.PingTimeouts);
        Assert.Equal(0, _metrics.CommandsThrottled);
        Assert.Equal(0, _metrics.ExcessFloodDisconnects);
    }

    [Fact]
    public void ConnectionAccepted_IncrementsConnectionsAccepted()
    {
        _metrics.ConnectionAccepted();
        _metrics.ConnectionAccepted();
        _metrics.ConnectionAccepted();

        Assert.Equal(3, _metrics.ConnectionsAccepted);
    }

    [Fact]
    public void ConnectionActive_And_ConnectionClosed_TracksActiveCount()
    {
        _metrics.ConnectionActive();
        _metrics.ConnectionActive();
        _metrics.ConnectionActive();
        Assert.Equal(3, _metrics.ConnectionsActive);

        _metrics.ConnectionClosed();
        Assert.Equal(2, _metrics.ConnectionsActive);

        _metrics.ConnectionClosed();
        _metrics.ConnectionClosed();
        Assert.Equal(0, _metrics.ConnectionsActive);
    }

    [Fact]
    public void RegistrationCompleted_Increments()
    {
        _metrics.RegistrationCompleted();
        _metrics.RegistrationCompleted();

        Assert.Equal(2, _metrics.RegistrationsCompleted);
    }

    [Fact]
    public void PingTimeout_Increments()
    {
        _metrics.PingTimeout();
        _metrics.PingTimeout();
        _metrics.PingTimeout();

        Assert.Equal(3, _metrics.PingTimeouts);
    }

    [Fact]
    public void CommandThrottled_Increments()
    {
        _metrics.CommandThrottled();
        _metrics.CommandThrottled();

        Assert.Equal(2, _metrics.CommandsThrottled);
    }

    [Fact]
    public void ExcessFloodDisconnect_Increments()
    {
        _metrics.ExcessFloodDisconnect();

        Assert.Equal(1, _metrics.ExcessFloodDisconnects);
    }

    [Fact]
    public void GetSnapshot_ReturnsCurrentCounters()
    {
        _metrics.ConnectionAccepted();
        _metrics.ConnectionAccepted();
        _metrics.ConnectionActive();
        _metrics.RegistrationCompleted();
        _metrics.PingTimeout();
        _metrics.CommandThrottled();
        _metrics.ExcessFloodDisconnect();
        _metrics.CommandDispatched();
        _metrics.MessageBroadcast();
        _metrics.MessagePrivate();
        _metrics.DbWrite();
        _metrics.Error();

        var snapshot = _metrics.GetSnapshot();

        Assert.Equal(2, snapshot.ConnectionsAccepted);
        Assert.Equal(1, snapshot.ConnectionsActive);
        Assert.Equal(1, snapshot.RegistrationsCompleted);
        Assert.Equal(1, snapshot.CommandsDispatched);
        Assert.Equal(1, snapshot.MessagesBroadcast);
        Assert.Equal(1, snapshot.MessagesPrivate);
        Assert.Equal(1, snapshot.DbWrites);
        Assert.Equal(1, snapshot.ErrorsTotal);
        Assert.Equal(1, snapshot.CommandsThrottled);
        Assert.Equal(1, snapshot.ExcessFloodDisconnects);
    }

    [Fact]
    public void RecordRegistrationDuration_ShowsInSnapshot()
    {
        _metrics.RecordRegistrationDuration(50.0);
        _metrics.RecordRegistrationDuration(100.0);

        var snapshot = _metrics.GetSnapshot();

        Assert.Equal(2, snapshot.RegistrationDuration.Count);
        Assert.Equal(150.0, snapshot.RegistrationDuration.Sum);
        Assert.Equal(50.0, snapshot.RegistrationDuration.Min);
        Assert.Equal(100.0, snapshot.RegistrationDuration.Max);
        Assert.Equal(75.0, snapshot.RegistrationDuration.Avg);
    }

    [Fact]
    public void RecordCommandDuration_TrackedPerCommand()
    {
        _metrics.RecordCommandDuration("PRIVMSG", 10.0);
        _metrics.RecordCommandDuration("PRIVMSG", 20.0);
        _metrics.RecordCommandDuration("JOIN", 5.0);

        var snapshot = _metrics.GetSnapshot();

        Assert.True(snapshot.CommandDuration.ContainsKey("PRIVMSG"));
        Assert.True(snapshot.CommandDuration.ContainsKey("JOIN"));
        Assert.Equal(2, snapshot.CommandDuration["PRIVMSG"].Count);
        Assert.Equal(1, snapshot.CommandDuration["JOIN"].Count);
        Assert.Equal(15.0, snapshot.CommandDuration["PRIVMSG"].Avg);
        Assert.Equal(5.0, snapshot.CommandDuration["JOIN"].Avg);
    }

    [Fact]
    public void HistogramTracker_RecordsMinMaxAvgCorrectly()
    {
        var tracker = new HistogramTracker();
        tracker.Record(10.0);
        tracker.Record(20.0);
        tracker.Record(30.0);
        tracker.Record(40.0);

        var snapshot = tracker.GetSnapshot();

        Assert.Equal(4, snapshot.Count);
        Assert.Equal(100.0, snapshot.Sum);
        Assert.Equal(10.0, snapshot.Min);
        Assert.Equal(40.0, snapshot.Max);
        Assert.Equal(25.0, snapshot.Avg);
        Assert.Equal(40.0, snapshot.Last);
    }

    [Fact]
    public void HistogramTracker_SingleValue_MinEqualsMaxEqualsAvg()
    {
        var tracker = new HistogramTracker();
        tracker.Record(42.0);

        var snapshot = tracker.GetSnapshot();

        Assert.Equal(1, snapshot.Count);
        Assert.Equal(42.0, snapshot.Min);
        Assert.Equal(42.0, snapshot.Max);
        Assert.Equal(42.0, snapshot.Avg);
        Assert.Equal(42.0, snapshot.Last);
    }

    [Fact]
    public void GetTimestamp_And_GetElapsedMs_ReturnPositiveValues()
    {
        var start = ServerMetrics.GetTimestamp();

        Assert.True(start > 0);

        // Perform a small amount of work to ensure elapsed time is positive
        var sum = 0;
        for (var i = 0; i < 1000; i++) sum += i;

        var elapsed = ServerMetrics.GetElapsedMs(start);

        Assert.True(elapsed >= 0, $"Expected non-negative elapsed time, but got {elapsed}");
        // Prevent the loop from being optimized away
        Assert.True(sum >= 0);
    }
}
