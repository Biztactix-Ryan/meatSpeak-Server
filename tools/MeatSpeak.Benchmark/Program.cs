using MeatSpeak.Benchmark;

var opts = BenchmarkOptions.Parse(args);

// Resolve seed: 0 means random
if (opts.Seed == 0)
    opts = opts with { Seed = Random.Shared.Next(1, int.MaxValue) };

using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    cts.Cancel();
    Console.WriteLine("\nCancelling...");
};

var runner = new BenchmarkRunner(opts);
var metrics = await runner.RunAsync(cts.Token);
metrics.Print();
