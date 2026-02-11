namespace MeatSpeak.Server.Voice;

public sealed class SilenceDetector
{
    private const int SilenceThreshold = 3;

    public bool IsSilence(ReadOnlySpan<byte> opusPayload)
    {
        if (opusPayload.Length <= SilenceThreshold)
            return true;
        return false;
    }
}
