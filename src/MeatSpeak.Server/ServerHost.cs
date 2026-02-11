namespace MeatSpeak.Server;

using System.Net;
using MeatSpeak.Server.Core.Server;
using MeatSpeak.Server.Transport.Tcp;
using MeatSpeak.Server.Transport.Tls;
using MeatSpeak.Server.Transport.Pools;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

public sealed class ServerHost : IHostedService
{
    private readonly IServer _server;
    private readonly IrcConnectionHandler _connectionHandler;
    private readonly ILogger<ServerHost> _logger;
    private readonly ICertificateProvider? _certProvider;
    private TcpServer? _tcpServer;
    private TlsTcpServer? _tlsTcpServer;

    public ServerHost(
        IServer server,
        IrcConnectionHandler connectionHandler,
        ILogger<ServerHost> logger,
        ICertificateProvider? certProvider = null)
    {
        _server = server;
        _connectionHandler = connectionHandler;
        _logger = logger;
        _certProvider = certProvider;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        var config = _server.Config;

        // Start TCP server
        var sendPool = new SocketEventArgsPool(4096, preAllocate: 32);
        _tcpServer = new TcpServer(_connectionHandler, _logger as ILogger<TcpServer> ??
            LoggerFactory.Create(b => b.AddConsole()).CreateLogger<TcpServer>(), sendPool);

        var tcpEndPoint = new IPEndPoint(IPAddress.Parse(config.TcpBindAddress), config.TcpPort);
        _tcpServer.Start(tcpEndPoint);

        _logger.LogInformation("MeatSpeak server started on {Endpoint}", tcpEndPoint);
        _logger.LogInformation("Server name: {Name}, Network: {Network}, Version: {Version}",
            config.ServerName, config.NetworkName, config.Version);

        // Start TLS TCP server if enabled
        if (config.Tls.Enabled && _certProvider != null)
        {
            _tlsTcpServer = new TlsTcpServer(
                _connectionHandler, _certProvider,
                _logger as ILogger<TlsTcpServer> ??
                    LoggerFactory.Create(b => b.AddConsole()).CreateLogger<TlsTcpServer>());

            var tlsEndPoint = new IPEndPoint(IPAddress.Parse(config.TcpBindAddress), config.Tls.IrcTlsPort);
            _tlsTcpServer.Start(tlsEndPoint);

            _logger.LogInformation("IRC TLS server started on {Endpoint}", tlsEndPoint);
        }

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("MeatSpeak server shutting down...");

        // Disconnect all sessions
        foreach (var session in _server.Sessions.Values)
        {
            try { session.DisconnectAsync("Server shutting down").AsTask().Wait(TimeSpan.FromSeconds(1)); }
            catch { }
        }

        _tlsTcpServer?.Dispose();
        _tcpServer?.Dispose();
        _logger.LogInformation("MeatSpeak server stopped.");
        return Task.CompletedTask;
    }
}
