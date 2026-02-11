using System.Diagnostics;

namespace MeatSpeak.Benchmark;

public sealed class BenchmarkRunner
{
    private readonly BenchmarkOptions _opts;
    private int _completedUsers;
    private int _totalActions;
    private int _totalErrors;
    private int _activeUsers;

    public BenchmarkRunner(BenchmarkOptions opts)
    {
        _opts = opts;
    }

    public async Task<AggregateMetrics> RunAsync(CancellationToken ct)
    {
        Console.WriteLine($"MeatSpeak Benchmark — {_opts.Users} users, concurrency {_opts.Concurrency}, seed {_opts.Seed}");
        Console.WriteLine($"Target: {_opts.Transport}://{_opts.Host}:{_opts.Port}");
        Console.WriteLine($"Actions/user: {_opts.Actions}, channels: {_opts.Channels}, delay: {_opts.Delay}ms");
        Console.WriteLine();

        var semaphore = new SemaphoreSlim(_opts.Concurrency);
        var allMetrics = new List<UserMetrics>(_opts.Users);
        var metricsLock = new object();
        var sw = Stopwatch.StartNew();

        using var progressCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var progressTask = Task.Run(() => PrintProgressLoop(sw, progressCts.Token), ct);

        var tasks = new Task[_opts.Users];
        for (int i = 0; i < _opts.Users; i++)
        {
            var userId = i;
            tasks[i] = Task.Run(async () =>
            {
                await semaphore.WaitAsync(ct);
                Interlocked.Increment(ref _activeUsers);
                try
                {
                    var user = new SimulatedUser(userId, _opts);
                    await user.RunAsync(ct);

                    lock (metricsLock)
                        allMetrics.Add(user.Metrics);

                    Interlocked.Add(ref _totalActions, user.Metrics.ActionsCompleted);
                    Interlocked.Add(ref _totalErrors, user.Metrics.Errors);
                    Interlocked.Increment(ref _completedUsers);
                }
                finally
                {
                    Interlocked.Decrement(ref _activeUsers);
                    semaphore.Release();
                }
            }, ct);
        }

        await Task.WhenAll(tasks);
        sw.Stop();

        progressCts.Cancel();
        try { await progressTask; } catch { /* expected */ }

        // Final progress line
        Console.Write("\r" + new string(' ', 80) + "\r");
        Console.WriteLine($"[{sw.Elapsed.TotalSeconds:F0}s] {_completedUsers}/{_opts.Users} users complete | {_totalActions:N0} actions | {_totalErrors} errors | done");

        return AggregateMetrics.Compute(_opts, allMetrics, sw.Elapsed.TotalSeconds);
    }

    private async Task PrintProgressLoop(Stopwatch sw, CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested)
            {
                await Task.Delay(1000, ct);
                var elapsed = sw.Elapsed.TotalSeconds;
                Console.Write($"\r[{elapsed,4:F0}s] {_completedUsers}/{_opts.Users} users complete | {_totalActions:N0} actions | {_totalErrors} errors | {_activeUsers} active    ");
            }
        }
        catch (OperationCanceledException) { }
    }
}
