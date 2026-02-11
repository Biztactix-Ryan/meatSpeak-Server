namespace MeatSpeak.Server.Diagnostics;

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.Metrics;

public sealed class ServerMetrics
{
    private readonly Meter _meter = new("MeatSpeak.Server");

    // Counters
    private readonly Counter<long> _connectionsAccepted;
    private readonly UpDownCounter<long> _connectionsActive;
    private readonly Counter<long> _registrationsCompleted;
    private readonly Counter<long> _commandsDispatched;
    private readonly Counter<long> _messagesBroadcast;
    private readonly Counter<long> _messagesPrivate;
    private readonly Counter<long> _dbWrites;
    private readonly Counter<long> _errorsTotal;

    // Histograms (System.Diagnostics.Metrics)
    private readonly Histogram<double> _registrationDuration;
    private readonly Histogram<double> _commandDuration;
    private readonly Histogram<double> _broadcastDuration;
    private readonly Histogram<double> _nickLookupDuration;
    private readonly Histogram<double> _dbWriteDuration;

    // Snapshot trackers (since Histogram<T> doesn't expose aggregated values)
    private readonly HistogramTracker _registrationDurationTracker = new();
    private readonly ConcurrentDictionary<string, HistogramTracker> _commandDurationTrackers = new(StringComparer.OrdinalIgnoreCase);
    private readonly HistogramTracker _broadcastDurationTracker = new();
    private readonly HistogramTracker _nickLookupDurationTracker = new();
    private readonly ConcurrentDictionary<string, HistogramTracker> _dbWriteDurationTrackers = new(StringComparer.OrdinalIgnoreCase);

    // Public read-only access for polling (e.g. BenchmarkService)
    public long ConnectionsAccepted => Interlocked.Read(ref _connectionsAcceptedValue);
    public long ConnectionsActive => Interlocked.Read(ref _connectionsActiveValue);

    // Counter snapshot values
    private long _connectionsAcceptedValue;
    private long _connectionsActiveValue;
    private long _registrationsCompletedValue;
    private long _commandsDispatchedValue;
    private long _messagesBroadcastValue;
    private long _messagesPrivateValue;
    private long _dbWritesValue;
    private long _errorsTotalValue;

    public ServerMetrics()
    {
        _connectionsAccepted = _meter.CreateCounter<long>("connections.accepted");
        _connectionsActive = _meter.CreateUpDownCounter<long>("connections.active");
        _registrationsCompleted = _meter.CreateCounter<long>("registrations.completed");
        _commandsDispatched = _meter.CreateCounter<long>("commands.dispatched");
        _messagesBroadcast = _meter.CreateCounter<long>("messages.broadcast");
        _messagesPrivate = _meter.CreateCounter<long>("messages.private");
        _dbWrites = _meter.CreateCounter<long>("db.writes");
        _errorsTotal = _meter.CreateCounter<long>("errors.total");

        _registrationDuration = _meter.CreateHistogram<double>("registration.duration_ms");
        _commandDuration = _meter.CreateHistogram<double>("command.duration_ms");
        _broadcastDuration = _meter.CreateHistogram<double>("broadcast.duration_ms");
        _nickLookupDuration = _meter.CreateHistogram<double>("nick_lookup.duration_ms");
        _dbWriteDuration = _meter.CreateHistogram<double>("db.write_duration_ms");
    }

    // --- Counter methods ---

    public void ConnectionAccepted()
    {
        _connectionsAccepted.Add(1);
        Interlocked.Increment(ref _connectionsAcceptedValue);
    }

    public void ConnectionActive()
    {
        _connectionsActive.Add(1);
        Interlocked.Increment(ref _connectionsActiveValue);
    }

    public void ConnectionClosed()
    {
        _connectionsActive.Add(-1);
        Interlocked.Decrement(ref _connectionsActiveValue);
    }

    public void RegistrationCompleted()
    {
        _registrationsCompleted.Add(1);
        Interlocked.Increment(ref _registrationsCompletedValue);
    }

    public void CommandDispatched()
    {
        _commandsDispatched.Add(1);
        Interlocked.Increment(ref _commandsDispatchedValue);
    }

    public void MessageBroadcast()
    {
        _messagesBroadcast.Add(1);
        Interlocked.Increment(ref _messagesBroadcastValue);
    }

    public void MessagePrivate()
    {
        _messagesPrivate.Add(1);
        Interlocked.Increment(ref _messagesPrivateValue);
    }

    public void DbWrite()
    {
        _dbWrites.Add(1);
        Interlocked.Increment(ref _dbWritesValue);
    }

    public void Error()
    {
        _errorsTotal.Add(1);
        Interlocked.Increment(ref _errorsTotalValue);
    }

    // --- Histogram methods ---

    public void RecordRegistrationDuration(double ms)
    {
        _registrationDuration.Record(ms);
        _registrationDurationTracker.Record(ms);
    }

    public void RecordCommandDuration(string command, double ms)
    {
        _commandDuration.Record(ms, new KeyValuePair<string, object?>("command", command));
        var tracker = _commandDurationTrackers.GetOrAdd(command, _ => new HistogramTracker());
        tracker.Record(ms);
    }

    public void RecordBroadcastDuration(double ms)
    {
        _broadcastDuration.Record(ms);
        _broadcastDurationTracker.Record(ms);
    }

    public void RecordNickLookupDuration(double ms)
    {
        _nickLookupDuration.Record(ms);
        _nickLookupDurationTracker.Record(ms);
    }

    public void RecordDbWriteDuration(string operation, double ms)
    {
        _dbWriteDuration.Record(ms, new KeyValuePair<string, object?>("operation", operation));
        var tracker = _dbWriteDurationTrackers.GetOrAdd(operation, _ => new HistogramTracker());
        tracker.Record(ms);
    }

    // --- Snapshot ---

    public MetricsSnapshot GetSnapshot()
    {
        var commandHistograms = new Dictionary<string, HistogramSnapshot>(StringComparer.OrdinalIgnoreCase);
        foreach (var (cmd, tracker) in _commandDurationTrackers)
            commandHistograms[cmd] = tracker.GetSnapshot();

        var dbWriteHistograms = new Dictionary<string, HistogramSnapshot>(StringComparer.OrdinalIgnoreCase);
        foreach (var (op, tracker) in _dbWriteDurationTrackers)
            dbWriteHistograms[op] = tracker.GetSnapshot();

        return new MetricsSnapshot
        {
            ConnectionsAccepted = Interlocked.Read(ref _connectionsAcceptedValue),
            ConnectionsActive = Interlocked.Read(ref _connectionsActiveValue),
            RegistrationsCompleted = Interlocked.Read(ref _registrationsCompletedValue),
            CommandsDispatched = Interlocked.Read(ref _commandsDispatchedValue),
            MessagesBroadcast = Interlocked.Read(ref _messagesBroadcastValue),
            MessagesPrivate = Interlocked.Read(ref _messagesPrivateValue),
            DbWrites = Interlocked.Read(ref _dbWritesValue),
            ErrorsTotal = Interlocked.Read(ref _errorsTotalValue),
            RegistrationDuration = _registrationDurationTracker.GetSnapshot(),
            CommandDuration = commandHistograms,
            BroadcastDuration = _broadcastDurationTracker.GetSnapshot(),
            NickLookupDuration = _nickLookupDurationTracker.GetSnapshot(),
            DbWriteDuration = dbWriteHistograms,
        };
    }

    // --- Timing helpers ---

    public static long GetTimestamp() => Stopwatch.GetTimestamp();

    public static double GetElapsedMs(long startTimestamp)
        => Stopwatch.GetElapsedTime(startTimestamp).TotalMilliseconds;
}

public sealed class HistogramTracker
{
    private long _count;
    private double _sum;
    private double _min = double.MaxValue;
    private double _max = double.MinValue;
    private double _last;
    private readonly object _lock = new();

    // Ring buffer for percentile approximation
    private const int BufferSize = 1024;
    private readonly double[] _buffer = new double[BufferSize];
    private int _bufferIndex;
    private int _bufferCount;

    public void Record(double value)
    {
        lock (_lock)
        {
            _count++;
            _sum += value;
            if (value < _min) _min = value;
            if (value > _max) _max = value;
            _last = value;

            _buffer[_bufferIndex] = value;
            _bufferIndex = (_bufferIndex + 1) % BufferSize;
            if (_bufferCount < BufferSize) _bufferCount++;
        }
    }

    public HistogramSnapshot GetSnapshot()
    {
        lock (_lock)
        {
            if (_count == 0)
                return new HistogramSnapshot();

            // Sort the ring buffer contents for percentile calculation
            var count = _bufferCount;
            var sorted = new double[count];
            Array.Copy(_buffer, sorted, count);
            Array.Sort(sorted);

            return new HistogramSnapshot
            {
                Count = _count,
                Sum = _sum,
                Min = _min,
                Max = _max,
                Last = _last,
                Avg = _sum / _count,
                P50 = Percentile(sorted, 0.50),
                P90 = Percentile(sorted, 0.90),
                P95 = Percentile(sorted, 0.95),
                P99 = Percentile(sorted, 0.99),
            };
        }
    }

    private static double Percentile(double[] sorted, double p)
    {
        if (sorted.Length == 0) return 0;
        var index = (int)Math.Ceiling(p * sorted.Length) - 1;
        return sorted[Math.Max(0, index)];
    }
}

public sealed class HistogramSnapshot
{
    public long Count { get; init; }
    public double Sum { get; init; }
    public double Min { get; init; }
    public double Max { get; init; }
    public double Last { get; init; }
    public double Avg { get; init; }
    public double P50 { get; init; }
    public double P90 { get; init; }
    public double P95 { get; init; }
    public double P99 { get; init; }
}

public sealed class MetricsSnapshot
{
    public long ConnectionsAccepted { get; init; }
    public long ConnectionsActive { get; init; }
    public long RegistrationsCompleted { get; init; }
    public long CommandsDispatched { get; init; }
    public long MessagesBroadcast { get; init; }
    public long MessagesPrivate { get; init; }
    public long DbWrites { get; init; }
    public long ErrorsTotal { get; init; }
    public HistogramSnapshot RegistrationDuration { get; init; } = new();
    public Dictionary<string, HistogramSnapshot> CommandDuration { get; init; } = new();
    public HistogramSnapshot BroadcastDuration { get; init; } = new();
    public HistogramSnapshot NickLookupDuration { get; init; } = new();
    public Dictionary<string, HistogramSnapshot> DbWriteDuration { get; init; } = new();
}
