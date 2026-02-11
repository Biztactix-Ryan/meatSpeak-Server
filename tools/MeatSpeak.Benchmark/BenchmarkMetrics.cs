using System.Diagnostics;

namespace MeatSpeak.Benchmark;

public sealed class UserMetrics
{
    public int UserId { get; init; }
    public long ConnectTimeMs { get; set; }
    public int ActionsCompleted { get; set; }
    public int Errors { get; set; }
    public long BytesSent { get; set; }
    public long BytesReceived { get; set; }
    public List<long> ActionTimesMs { get; } = new();
}

public sealed class AggregateMetrics
{
    public int Seed { get; init; }
    public string Transport { get; init; } = "";
    public int TotalUsers { get; init; }
    public int Concurrency { get; init; }
    public int ActionsPerUser { get; init; }
    public int TotalActions { get; set; }
    public int TotalErrors { get; set; }
    public double DurationSeconds { get; set; }
    public long BytesSent { get; set; }
    public long BytesReceived { get; set; }
    public long ConnectP50 { get; set; }
    public long ConnectP95 { get; set; }
    public long ConnectP99 { get; set; }
    public long ActionP50 { get; set; }
    public long ActionP95 { get; set; }
    public long ActionP99 { get; set; }

    public double Throughput => DurationSeconds > 0 ? TotalActions / DurationSeconds : 0;
    public double ErrorRate => TotalActions > 0 ? (double)TotalErrors / (TotalActions + TotalErrors) * 100 : 0;

    public static AggregateMetrics Compute(BenchmarkOptions opts, List<UserMetrics> users, double durationSeconds)
    {
        var agg = new AggregateMetrics
        {
            Seed = opts.Seed,
            Transport = opts.Transport,
            TotalUsers = opts.Users,
            Concurrency = opts.Concurrency,
            ActionsPerUser = opts.Actions,
            DurationSeconds = durationSeconds,
        };

        var connectTimes = new List<long>(users.Count);
        var actionTimes = new List<long>();

        foreach (var u in users)
        {
            agg.TotalActions += u.ActionsCompleted;
            agg.TotalErrors += u.Errors;
            agg.BytesSent += u.BytesSent;
            agg.BytesReceived += u.BytesReceived;
            connectTimes.Add(u.ConnectTimeMs);
            actionTimes.AddRange(u.ActionTimesMs);
        }

        connectTimes.Sort();
        actionTimes.Sort();

        if (connectTimes.Count > 0)
        {
            agg.ConnectP50 = Percentile(connectTimes, 50);
            agg.ConnectP95 = Percentile(connectTimes, 95);
            agg.ConnectP99 = Percentile(connectTimes, 99);
        }

        if (actionTimes.Count > 0)
        {
            agg.ActionP50 = Percentile(actionTimes, 50);
            agg.ActionP95 = Percentile(actionTimes, 95);
            agg.ActionP99 = Percentile(actionTimes, 99);
        }

        return agg;
    }

    private static long Percentile(List<long> sorted, int p)
    {
        if (sorted.Count == 0) return 0;
        double index = (p / 100.0) * (sorted.Count - 1);
        int lower = (int)Math.Floor(index);
        int upper = (int)Math.Ceiling(index);
        if (lower == upper) return sorted[lower];
        double frac = index - lower;
        return (long)(sorted[lower] * (1 - frac) + sorted[upper] * frac);
    }

    public void Print()
    {
        static string FormatBytes(long bytes)
        {
            if (bytes >= 1024 * 1024) return $"{bytes / (1024.0 * 1024.0):F1} MB";
            if (bytes >= 1024) return $"{bytes / 1024.0:F1} KB";
            return $"{bytes} B";
        }

        Console.WriteLine();
        Console.WriteLine("── MeatSpeak Benchmark Results ──────────────────────────");
        Console.WriteLine($"  Seed:           {Seed}");
        Console.WriteLine($"  Transport:      {Transport}");
        Console.WriteLine($"  Total users:    {TotalUsers}");
        Console.WriteLine($"  Concurrency:    {Concurrency}");
        Console.WriteLine($"  Actions/user:   {ActionsPerUser}");
        Console.WriteLine();
        Console.WriteLine($"  Total actions:  {TotalActions:N0}");
        Console.WriteLine($"  Total errors:   {TotalErrors:N0} ({ErrorRate:F2}%)");
        Console.WriteLine($"  Duration:       {DurationSeconds:F1}s");
        Console.WriteLine($"  Throughput:     {Throughput:F0} actions/s");
        Console.WriteLine();
        Console.WriteLine($"  Connect time:   p50={ConnectP50}ms  p95={ConnectP95}ms  p99={ConnectP99}ms");
        Console.WriteLine($"  Action time:    p50={ActionP50}ms  p95={ActionP95}ms  p99={ActionP99}ms");
        Console.WriteLine($"  Bytes sent:     {FormatBytes(BytesSent)}");
        Console.WriteLine($"  Bytes received: {FormatBytes(BytesReceived)}");
        Console.WriteLine("──────────────────────────────────────────────────────────");
    }
}
