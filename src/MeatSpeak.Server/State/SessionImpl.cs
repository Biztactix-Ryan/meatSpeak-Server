namespace MeatSpeak.Server.State;

using System.Text;
using System.Threading.Channels;
using MeatSpeak.Protocol;
using MeatSpeak.Server.Core.Sessions;
using MeatSpeak.Server.Permissions;
using MeatSpeak.Server.Transport;

public sealed class SessionImpl : ISession
{
    private readonly IConnection _connection;
    private readonly string _serverName;
    private readonly Channel<Func<Task>> _commandQueue = Channel.CreateBounded<Func<Task>>(
        new BoundedChannelOptions(128) { FullMode = BoundedChannelFullMode.DropOldest });

    public string Id => _connection.Id;
    public SessionState State { get; set; } = SessionState.Connecting;
    public SessionInfo Info { get; } = new();
    public ServerPermission CachedServerPermissions { get; set; }
    public IConnection Connection => _connection;
    public ChannelWriter<Func<Task>> CommandWriter => _commandQueue.Writer;

    public SessionImpl(IConnection connection, string serverName)
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
        Info.LabeledMessageCount++;
        return ValueTask.CompletedTask;
    }

    public ValueTask SendTaggedMessageAsync(string? tags, string? prefix, string command, params string[] parameters)
    {
        Span<byte> buffer = stackalloc byte[IrcConstants.MaxLineLengthWithTags];
        int written = MessageBuilder.WriteWithTags(buffer, tags, prefix, command, parameters);
        _connection.Send(buffer[..written]);
        Info.LabeledMessageCount++;
        return ValueTask.CompletedTask;
    }

    public ValueTask SendNumericAsync(string serverName, int numeric, params string[] parameters)
    {
        Span<byte> buffer = stackalloc byte[IrcConstants.MaxLineLengthWithTags];
        var target = Info.Nickname ?? "*";
        int written = MessageBuilder.WriteNumeric(buffer, serverName, numeric, target, parameters);
        _connection.Send(buffer[..written]);
        Info.LabeledMessageCount++;
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

    public void StartCommandProcessing()
    {
        _ = Task.Run(async () =>
        {
            await foreach (var workItem in _commandQueue.Reader.ReadAllAsync())
            {
                try
                {
                    await workItem();
                }
                catch (Exception)
                {
                    // Errors are handled inside the work items
                }
            }
        });
    }

    public void StopCommandProcessing()
    {
        _commandQueue.Writer.TryComplete();
    }
}
