namespace MeatSpeak.Server.Diagnostics;

using System.Text.Json;
using MeatSpeak.Server.AdminApi.Methods;

public sealed class MetricsSnapshotMethod : IAdminMethod
{
    private readonly ServerMetrics _metrics;
    public string Name => "metrics.snapshot";

    public MetricsSnapshotMethod(ServerMetrics metrics) => _metrics = metrics;

    public Task<object?> ExecuteAsync(JsonElement? parameters, CancellationToken ct = default)
    {
        var snapshot = _metrics.GetSnapshot();
        return Task.FromResult<object?>(new
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
                commands_throttled = snapshot.CommandsThrottled,
                excess_flood_disconnects = snapshot.ExcessFloodDisconnects,
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
}
