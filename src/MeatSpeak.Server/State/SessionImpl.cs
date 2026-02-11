namespace MeatSpeak.Server.State;

using System.Text;
using MeatSpeak.Protocol;
using MeatSpeak.Server.Core.Sessions;
using MeatSpeak.Server.Permissions;
using MeatSpeak.Server.Transport.Tcp;

public sealed class SessionImpl : ISession
{
    private readonly TcpConnection _connection;
    private readonly string _serverName;

    public string Id => _connection.Id;
    public SessionState State { get; set; } = SessionState.Connecting;
    public SessionInfo Info { get; } = new();
    public ServerPermission CachedServerPermissions { get; set; }
    public TcpConnection Connection => _connection;

    public SessionImpl(TcpConnection connection, string serverName)
    {
        _connection = connection;
        _serverName = serverName;
        Info.Hostname = connection.RemoteEndPoint?.ToString()?.Split(':')[0] ?? "unknown";
    }

    public ValueTask SendAsync(ReadOnlyMemory<byte> data, CancellationToken ct = default)
    {
        _connection.Send(data.Span);
        return ValueTask.CompletedTask;
    }

    public ValueTask SendLineAsync(string line, CancellationToken ct = default)
    {
        var bytes = Encoding.UTF8.GetBytes(line + "\r\n");
        _connection.Send(bytes);
        return ValueTask.CompletedTask;
    }

    public ValueTask SendMessageAsync(string? prefix, string command, params string[] parameters)
    {
        Span<byte> buffer = stackalloc byte[IrcConstants.MaxLineLengthWithTags];
        int written = MessageBuilder.Write(buffer, prefix, command, parameters);
        _connection.Send(buffer[..written]);
        return ValueTask.CompletedTask;
    }

    public ValueTask SendNumericAsync(string serverName, int numeric, params string[] parameters)
    {
        Span<byte> buffer = stackalloc byte[IrcConstants.MaxLineLengthWithTags];
        var target = Info.Nickname ?? "*";
        int written = MessageBuilder.WriteNumeric(buffer, serverName, numeric, target, parameters);
        _connection.Send(buffer[..written]);
        return ValueTask.CompletedTask;
    }

    public ValueTask DisconnectAsync(string? reason = null)
    {
        State = SessionState.Disconnecting;
        if (reason != null)
        {
            var errorMsg = $"ERROR :Closing Link: {Info.Hostname} ({reason})";
            SendLineAsync(errorMsg).AsTask().Wait();
        }
        _connection.Disconnect();
        return ValueTask.CompletedTask;
    }
}
