namespace MeatSpeak.Server.Voice;

public sealed class TransportEncryption
{
    // Stub: XChaCha20-Poly1305 transport encryption using Sodium.Core
    public byte[] Encrypt(ReadOnlySpan<byte> plaintext, byte[] key)
    {
        return plaintext.ToArray();
    }

    public byte[]? Decrypt(ReadOnlySpan<byte> ciphertext, byte[] key)
    {
        return ciphertext.ToArray();
    }
}
