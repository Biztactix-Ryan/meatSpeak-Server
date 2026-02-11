namespace MeatSpeak.Server.Core.Sessions;

using MeatSpeak.Protocol;
using MeatSpeak.Server.Permissions;

public interface ISession
{
    string Id { get; }
    SessionState State { get; set; }
    SessionInfo Info { get; }
    ServerPermission CachedServerPermissions { get; set; }

    ValueTask SendAsync(ReadOnlyMemory<byte> data, CancellationToken ct = default);
    ValueTask SendLineAsync(string line, CancellationToken ct = default);
    ValueTask SendMessageAsync(string? prefix, string command, params string[] parameters);
    ValueTask SendNumericAsync(string serverName, int numeric, params string[] parameters);
    ValueTask DisconnectAsync(string? reason = null);
}
