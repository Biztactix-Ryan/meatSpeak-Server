using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using MeatSpeak.Server.Transport;
using MeatSpeak.Server.Transport.Tls;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace MeatSpeak.Server.Tests;

public class TlsTcpConnectionTests
{
    private static X509Certificate2 CreateSelfSignedCert()
    {
        using var rsa = RSA.Create(2048);
        var req = new CertificateRequest("CN=localhost", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        var cert = req.CreateSelfSigned(DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddDays(1));
        // Export and re-import for SslStream compatibility
        var pfx = cert.Export(X509ContentType.Pfx);
        return X509CertificateLoader.LoadPkcs12(pfx, null);
    }

    private sealed class TestConnectionHandler : IConnectionHandler
    {
        public readonly List<string> Events = new();
        public readonly List<string> ReceivedLines = new();
        public IConnection? LastConnection { get; private set; }
        private readonly TaskCompletionSource _connectedTcs = new();
        private readonly TaskCompletionSource _disconnectedTcs = new();
        private readonly TaskCompletionSource<string> _firstLineTcs = new();

        public Task WaitForConnected(CancellationToken ct = default)
        {
            ct.Register(() => _connectedTcs.TrySetCanceled());
            return _connectedTcs.Task;
        }

        public Task WaitForDisconnected(CancellationToken ct = default)
        {
            ct.Register(() => _disconnectedTcs.TrySetCanceled());
            return _disconnectedTcs.Task;
        }

        public Task<string> WaitForFirstLine(CancellationToken ct = default)
        {
            ct.Register(() => _firstLineTcs.TrySetCanceled());
            return _firstLineTcs.Task;
        }

        public void OnConnected(IConnection connection)
        {
            LastConnection = connection;
            Events.Add("connected");
            _connectedTcs.TrySetResult();
        }

        public void OnData(IConnection connection, ReadOnlySpan<byte> line)
        {
            var text = Encoding.UTF8.GetString(line);
            ReceivedLines.Add(text);
            _firstLineTcs.TrySetResult(text);
        }

        public void OnDisconnected(IConnection connection)
        {
            Events.Add("disconnected");
            _disconnectedTcs.TrySetResult();
        }
    }

    [Fact]
    public void TlsTcpConnection_HasCorrectIdPrefix()
    {
        // Create a connected socket pair for testing
        using var listener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        listener.Bind(new IPEndPoint(IPAddress.Loopback, 0));
        listener.Listen(1);
        var port = ((IPEndPoint)listener.LocalEndPoint!).Port;

        using var clientSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        clientSocket.Connect(IPAddress.Loopback, port);
        using var serverSocket = listener.Accept();

        var handler = new TestConnectionHandler();
        var networkStream = new NetworkStream(serverSocket, ownsSocket: false);
        var sslStream = new SslStream(networkStream, leaveInnerStreamOpen: false);

        using var conn = new TlsTcpConnection(serverSocket, sslStream, handler, NullLogger.Instance);
        Assert.StartsWith("tls-", conn.Id);
    }

    [Fact]
    public void TlsTcpConnection_RemoteEndPoint_IsPreserved()
    {
        using var listener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        listener.Bind(new IPEndPoint(IPAddress.Loopback, 0));
        listener.Listen(1);
        var port = ((IPEndPoint)listener.LocalEndPoint!).Port;

        using var clientSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        clientSocket.Connect(IPAddress.Loopback, port);
        using var serverSocket = listener.Accept();

        var handler = new TestConnectionHandler();
        var networkStream = new NetworkStream(serverSocket, ownsSocket: false);
        var sslStream = new SslStream(networkStream, leaveInnerStreamOpen: false);

        using var conn = new TlsTcpConnection(serverSocket, sslStream, handler, NullLogger.Instance);
        Assert.NotNull(conn.RemoteEndPoint);
        Assert.IsType<IPEndPoint>(conn.RemoteEndPoint);
    }

    [Fact]
    public async Task RunAsync_ClientSendsLine_HandlerReceivesLine()
    {
        using var cert = CreateSelfSignedCert();
        var handler = new TestConnectionHandler();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        // Set up TCP listener
        using var listener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        listener.Bind(new IPEndPoint(IPAddress.Loopback, 0));
        listener.Listen(1);
        var port = ((IPEndPoint)listener.LocalEndPoint!).Port;

        // Connect client
        using var clientSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        await clientSocket.ConnectAsync(IPAddress.Loopback, port, cts.Token);
        using var serverSocket = await listener.AcceptAsync(cts.Token);

        // Server side TLS
        var serverNetStream = new NetworkStream(serverSocket, ownsSocket: false);
        var serverSsl = new SslStream(serverNetStream, leaveInnerStreamOpen: false);
        var serverAuthTask = serverSsl.AuthenticateAsServerAsync(cert);

        // Client side TLS
        var clientNetStream = new NetworkStream(clientSocket, ownsSocket: false);
        var clientSsl = new SslStream(clientNetStream, leaveInnerStreamOpen: false,
            (sender, certificate, chain, errors) => true);
        var clientAuthTask = clientSsl.AuthenticateAsClientAsync("localhost");

        await Task.WhenAll(serverAuthTask, clientAuthTask);

        var conn = new TlsTcpConnection(serverSocket, serverSsl, handler, NullLogger.Instance);
        var runTask = conn.RunAsync(cts.Token);

        await handler.WaitForConnected(cts.Token);

        // Send data from client
        var data = Encoding.UTF8.GetBytes("NICK testuser\r\n");
        await clientSsl.WriteAsync(data, cts.Token);
        await clientSsl.FlushAsync(cts.Token);

        var line = await handler.WaitForFirstLine(cts.Token);
        Assert.Equal("NICK testuser", line);

        // Close client to end receive loop
        clientSsl.Close();
        clientSocket.Close();
        await handler.WaitForDisconnected(cts.Token);

        conn.Dispose();
    }

    [Fact]
    public async Task Send_DeliversDataToClient()
    {
        using var cert = CreateSelfSignedCert();
        var handler = new TestConnectionHandler();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        using var listener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        listener.Bind(new IPEndPoint(IPAddress.Loopback, 0));
        listener.Listen(1);
        var port = ((IPEndPoint)listener.LocalEndPoint!).Port;

        using var clientSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        await clientSocket.ConnectAsync(IPAddress.Loopback, port, cts.Token);
        using var serverSocket = await listener.AcceptAsync(cts.Token);

        var serverNetStream = new NetworkStream(serverSocket, ownsSocket: false);
        var serverSsl = new SslStream(serverNetStream, leaveInnerStreamOpen: false);
        var serverAuthTask = serverSsl.AuthenticateAsServerAsync(cert);

        var clientNetStream = new NetworkStream(clientSocket, ownsSocket: false);
        var clientSsl = new SslStream(clientNetStream, leaveInnerStreamOpen: false,
            (sender, certificate, chain, errors) => true);
        var clientAuthTask = clientSsl.AuthenticateAsClientAsync("localhost");

        await Task.WhenAll(serverAuthTask, clientAuthTask);

        var conn = new TlsTcpConnection(serverSocket, serverSsl, handler, NullLogger.Instance);
        var runTask = conn.RunAsync(cts.Token);

        await handler.WaitForConnected(cts.Token);

        // Server sends data to client
        var sendData = Encoding.UTF8.GetBytes(":server PING :test\r\n");
        handler.LastConnection!.Send(sendData);

        // Read from client side
        var recvBuffer = new byte[4096];
        var bytesRead = await clientSsl.ReadAsync(recvBuffer, cts.Token);
        var received = Encoding.UTF8.GetString(recvBuffer, 0, bytesRead);
        Assert.Equal(":server PING :test\r\n", received);

        clientSsl.Close();
        clientSocket.Close();

        conn.Dispose();
    }

    [Fact]
    public async Task Disconnect_ServerSide_DisconnectsClient()
    {
        using var cert = CreateSelfSignedCert();
        var handler = new TestConnectionHandler();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        using var listener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        listener.Bind(new IPEndPoint(IPAddress.Loopback, 0));
        listener.Listen(1);
        var port = ((IPEndPoint)listener.LocalEndPoint!).Port;

        using var clientSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        await clientSocket.ConnectAsync(IPAddress.Loopback, port, cts.Token);
        using var serverSocket = await listener.AcceptAsync(cts.Token);

        var serverNetStream = new NetworkStream(serverSocket, ownsSocket: false);
        var serverSsl = new SslStream(serverNetStream, leaveInnerStreamOpen: false);
        var serverAuthTask = serverSsl.AuthenticateAsServerAsync(cert);

        var clientNetStream = new NetworkStream(clientSocket, ownsSocket: false);
        var clientSsl = new SslStream(clientNetStream, leaveInnerStreamOpen: false,
            (sender, certificate, chain, errors) => true);
        var clientAuthTask = clientSsl.AuthenticateAsClientAsync("localhost");

        await Task.WhenAll(serverAuthTask, clientAuthTask);

        var conn = new TlsTcpConnection(serverSocket, serverSsl, handler, NullLogger.Instance);
        var runTask = conn.RunAsync(cts.Token);

        await handler.WaitForConnected(cts.Token);

        // Server-side disconnect
        handler.LastConnection!.Disconnect();
        Assert.False(handler.LastConnection.IsConnected);
    }
}
