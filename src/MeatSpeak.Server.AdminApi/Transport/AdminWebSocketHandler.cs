namespace MeatSpeak.Server.AdminApi.Transport;

using MeatSpeak.Server.AdminApi.JsonRpc;
using MeatSpeak.Server.AdminApi.Auth;

public sealed class AdminWebSocketHandler
{
    private readonly JsonRpcProcessor _processor;
    private readonly ApiKeyAuthenticator _authenticator;

    public AdminWebSocketHandler(JsonRpcProcessor processor, ApiKeyAuthenticator authenticator)
    {
        _processor = processor;
        _authenticator = authenticator;
    }
}
