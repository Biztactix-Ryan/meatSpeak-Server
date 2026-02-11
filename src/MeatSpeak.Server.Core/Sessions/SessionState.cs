namespace MeatSpeak.Server.Core.Sessions;

public enum SessionState
{
    Connecting = 0,
    CapNegotiating = 1,
    Registering = 2,
    Registered = 3,
    Authenticated = 4,
    Disconnecting = 5,
}
