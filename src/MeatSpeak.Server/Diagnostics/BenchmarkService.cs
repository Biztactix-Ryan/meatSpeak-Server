namespace MeatSpeak.Server.Diagnostics;

using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

public sealed class BenchmarkService : IHostedService
{
    private readonly ServerMetrics _metrics;
    private readonly IHostApplicationLifetime _lifetime;
    private readonly ILogger<BenchmarkService> _logger;
    private readonly string? _outputPath;
    private CancellationTokenSource? _cts;
    private Task? _pollTask;

    public BenchmarkService(
        ServerMetrics metrics,
        IHostApplicationLifetime lifetime,
        ILogger<BenchmarkService> logger,
        string? outputPath)
    {
        _metrics = metrics;
        _lifetime = lifetime;
        _logger = logger;
        _outputPath = outputPath;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Benchmark mode enabled — server will auto-shutdown when all clients disconnect");
        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _pollTask = PollAsync(_cts.Token);
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _cts?.Cancel();
        if (_pollTask != null)
        {
            try { await _pollTask; }
            catch (OperationCanceledException) { }
        }
        _cts?.Dispose();
    }

    private async Task PollAsync(CancellationToken ct)
    {
        // Wait until at least one client completes registration (NICK/USER).
        // This avoids false triggers from health-check probes like `nc -z`.
        while (!ct.IsCancellationRequested && _metrics.RegistrationsCompleted == 0)
            await Task.Delay(500, ct);

        _logger.LogInformation("Benchmark: first client registered (registrations={Registrations}, active={Active})",
            _metrics.RegistrationsCompleted, _metrics.ConnectionsActive);

        // Wait until all clients have disconnected
        while (!ct.IsCancellationRequested && _metrics.ConnectionsActive > 0)
            await Task.Delay(500, ct);

        if (ct.IsCancellationRequested)
            return;

        // Grace period — re-check after 2 seconds to avoid false triggers during ramp-up
        _logger.LogInformation("Benchmark: all clients disconnected, waiting 2s grace period...");
        await Task.Delay(2000, ct);

        if (_metrics.ConnectionsActive > 0)
        {
            _logger.LogInformation("Benchmark: new clients connected during grace period, resuming poll");
            await PollAsync(ct); // Recurse back to waiting
            return;
        }

        _logger.LogInformation("Benchmark: grace period passed, shutting down (accepted={Accepted})",
            _metrics.ConnectionsAccepted);

        var snapshot = _metrics.GetSnapshot();

        if (!string.IsNullOrEmpty(_outputPath))
        {
            var json = FormatSnapshotJson(snapshot);
            await File.WriteAllTextAsync(_outputPath, json, ct);
            _logger.LogInformation("Benchmark: server metrics written to {Path}", _outputPath);
        }

        PrintSummary(snapshot);

        _lifetime.StopApplication();
    }

    private static string FormatSnapshotJson(MetricsSnapshot snapshot)
    {
        var obj = new
        {
            counters = new
            {
                connections_accepted = snapshot.ConnectionsAccepted,
                connections_active = snapshot.ConnectionsActive,
                registrations_completed = snapshot.RegistrationsCompleted,
                commands_dispatched = snapshot.CommandsDispatched,
                messages_broadcast = snapshot.MessagesBroadcast,
                messages_private = snapshot.MessagesPrivate,
                db_writes = snapshot.DbWrites,
                errors_total = snapshot.ErrorsTotal,
            },
            histograms = new
            {
                registration_duration_ms = FormatHistogram(snapshot.RegistrationDuration),
                command_duration_ms = snapshot.CommandDuration.ToDictionary(
                    kvp => kvp.Key,
                    kvp => FormatHistogram(kvp.Value)),
                broadcast_duration_ms = FormatHistogram(snapshot.BroadcastDuration),
                nick_lookup_duration_ms = FormatHistogram(snapshot.NickLookupDuration),
                db_write_duration_ms = snapshot.DbWriteDuration.ToDictionary(
                    kvp => kvp.Key,
                    kvp => FormatHistogram(kvp.Value)),
            },
        };

        return JsonSerializer.Serialize(obj, new JsonSerializerOptions
        {
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.Never,
        });
    }

    private static object FormatHistogram(HistogramSnapshot h) => new
    {
        count = h.Count,
        min = Math.Round(h.Min, 3),
        max = Math.Round(h.Max, 3),
        avg = Math.Round(h.Avg, 3),
        p50 = Math.Round(h.P50, 3),
        p90 = Math.Round(h.P90, 3),
        p95 = Math.Round(h.P95, 3),
        p99 = Math.Round(h.P99, 3),
        sum = Math.Round(h.Sum, 3),
    };

    private void PrintSummary(MetricsSnapshot s)
    {
        _logger.LogInformation("=== Benchmark Server Metrics ===");
        _logger.LogInformation("  Connections accepted: {V}", s.ConnectionsAccepted);
        _logger.LogInformation("  Registrations:        {V}", s.RegistrationsCompleted);
        _logger.LogInformation("  Commands dispatched:  {V}", s.CommandsDispatched);
        _logger.LogInformation("  Messages broadcast:   {V}", s.MessagesBroadcast);
        _logger.LogInformation("  Messages private:     {V}", s.MessagesPrivate);
        _logger.LogInformation("  Errors:               {V}", s.ErrorsTotal);
        if (s.RegistrationDuration.Count > 0)
            _logger.LogInformation("  Registration p50/p99: {P50:F1}ms / {P99:F1}ms",
                s.RegistrationDuration.P50, s.RegistrationDuration.P99);
        if (s.BroadcastDuration.Count > 0)
            _logger.LogInformation("  Broadcast p50/p99:    {P50:F1}ms / {P99:F1}ms",
                s.BroadcastDuration.P50, s.BroadcastDuration.P99);
    }
}
