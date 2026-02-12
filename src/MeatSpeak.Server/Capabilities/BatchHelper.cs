namespace MeatSpeak.Server.Capabilities;

using MeatSpeak.Protocol;
using MeatSpeak.Server.Core.Sessions;

public static class BatchHelper
{
    private static int _batchCounter;

    public static string GenerateReference()
        => Interlocked.Increment(ref _batchCounter).ToString("x8");

    public static async ValueTask StartBatch(ISession session, string reference, string type, params string[] parameters)
    {
        if (!CapHelper.HasCap(session, "batch"))
            return;

        var args = new string[2 + parameters.Length];
        args[0] = $"+{reference}";
        args[1] = type;
        for (int i = 0; i < parameters.Length; i++)
            args[i + 2] = parameters[i];

        await CapHelper.SendWithTimestamp(session, null, IrcConstants.BATCH, args);
    }

    public static async ValueTask EndBatch(ISession session, string reference)
    {
        if (!CapHelper.HasCap(session, "batch"))
            return;

        await CapHelper.SendWithTimestamp(session, null, IrcConstants.BATCH, $"-{reference}");
    }
}
