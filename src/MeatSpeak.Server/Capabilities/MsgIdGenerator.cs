namespace MeatSpeak.Server.Capabilities;

public static class MsgIdGenerator
{
    public static string Generate()
        => Guid.NewGuid().ToString("N");
}
