namespace MeatSpeak.Server.Voice;

using System.Security.Cryptography;

public sealed class TransportEncryption
{
    private const int NonceSize = 12;
    private const int TagSize = 16;

    public byte[] Encrypt(ReadOnlySpan<byte> plaintext, byte[] key)
    {
        var nonce = new byte[NonceSize];
        RandomNumberGenerator.Fill(nonce);

        var ciphertext = new byte[plaintext.Length];
        var tag = new byte[TagSize];

        using var aes = new AesGcm(key, TagSize);
        aes.Encrypt(nonce, plaintext, ciphertext, tag);

        // Return nonce + ciphertext + tag
        var result = new byte[NonceSize + ciphertext.Length + TagSize];
        nonce.CopyTo(result, 0);
        ciphertext.CopyTo(result, NonceSize);
        tag.CopyTo(result, NonceSize + ciphertext.Length);
        return result;
    }

    public byte[]? Decrypt(ReadOnlySpan<byte> ciphertext, byte[] key)
    {
        if (ciphertext.Length < NonceSize + TagSize)
            return null;

        var nonce = ciphertext[..NonceSize];
        var tag = ciphertext[^TagSize..];
        var encrypted = ciphertext[NonceSize..^TagSize];
        var plaintext = new byte[encrypted.Length];

        try
        {
            using var aes = new AesGcm(key, TagSize);
            aes.Decrypt(nonce, encrypted, tag, plaintext);
            return plaintext;
        }
        catch (CryptographicException)
        {
            return null;
        }
    }
}
