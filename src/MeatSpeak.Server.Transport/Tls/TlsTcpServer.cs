namespace MeatSpeak.Server.Transport.Tls;

using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.Logging;

public sealed class TlsTcpServer : IDisposable
{
    private readonly IConnectionHandler _handler;
    private readonly ICertificateProvider _certProvider;
    private readonly ILogger _logger;
    private Socket? _listenSocket;
    private CancellationTokenSource? _cts;
    private bool _disposed;

    public TlsTcpServer(
        IConnectionHandler handler,
        ICertificateProvider certProvider,
        ILogger logger)
    {
        _handler = handler;
        _certProvider = certProvider;
        _logger = logger;
    }

    public void Start(IPEndPoint endPoint, int backlog = 128)
    {
        _listenSocket = new Socket(endPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
        _listenSocket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
        _listenSocket.Bind(endPoint);
        _listenSocket.Listen(backlog);

        _cts = new CancellationTokenSource();
        _logger.LogInformation("TLS TCP server listening on {EndPoint}", endPoint);

        _ = AcceptLoopAsync(_cts.Token);
    }

    private async Task AcceptLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested && !_disposed)
        {
            Socket clientSocket;
            try
            {
                clientSocket = await _listenSocket!.AcceptAsync(ct);
            }
            catch (OperationCanceledException) { break; }
            catch (ObjectDisposedException) { break; }
            catch (Exception ex)
            {
                _logger.LogError(ex, "TLS accept error");
                continue;
            }

            _ = HandleClientAsync(clientSocket, ct);
        }
    }

    private async Task HandleClientAsync(Socket clientSocket, CancellationToken ct)
    {
        clientSocket.NoDelay = true;
        SslStream? sslStream = null;

        try
        {
            var cert = _certProvider.GetCertificate();
            if (cert == null)
            {
                _logger.LogWarning("No TLS certificate available, rejecting connection from {Remote}",
                    clientSocket.RemoteEndPoint);
                clientSocket.Close();
                return;
            }

            var networkStream = new NetworkStream(clientSocket, ownsSocket: false);
            sslStream = new SslStream(networkStream, leaveInnerStreamOpen: false);

            using var handshakeCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            handshakeCts.CancelAfter(TimeSpan.FromSeconds(10));

            await sslStream.AuthenticateAsServerAsync(new SslServerAuthenticationOptions
            {
                ServerCertificate = cert,
                EnabledSslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13,
                ClientCertificateRequired = false,
            }, handshakeCts.Token);

            _logger.LogDebug("TLS handshake completed with {Remote} using {Protocol}",
                clientSocket.RemoteEndPoint, sslStream.SslProtocol);

            var connection = new TlsTcpConnection(clientSocket, sslStream, _handler, _logger);
            await connection.RunAsync(ct);
        }
        catch (OperationCanceledException) { }
        catch (AuthenticationException ex)
        {
            _logger.LogDebug(ex, "TLS handshake failed with {Remote}", clientSocket.RemoteEndPoint);
        }
        catch (IOException ex)
        {
            _logger.LogDebug(ex, "TLS connection error with {Remote}", clientSocket.RemoteEndPoint);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error handling TLS client {Remote}", clientSocket.RemoteEndPoint);
        }
        finally
        {
            if (sslStream != null)
            {
                try { sslStream.Close(); } catch { }
                sslStream.Dispose();
            }
            try { clientSocket.Shutdown(SocketShutdown.Both); } catch { }
            try { clientSocket.Close(); } catch { }
            clientSocket.Dispose();
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _cts?.Cancel();
        _cts?.Dispose();
        try { _listenSocket?.Close(); } catch { }
        _listenSocket?.Dispose();
    }
}
