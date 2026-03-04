using System.Security.Cryptography;

namespace MeshBoard.Infrastructure.Meshtastic.Decoding;

internal static class AesCtrCipher
{
    private const int BlockSize = 16;

    public static byte[] Transform(ReadOnlySpan<byte> cipherText, ReadOnlySpan<byte> nonce, ReadOnlySpan<byte> key)
    {
        if (nonce.Length != BlockSize)
        {
            throw new ArgumentException("Meshtastic packet nonce must be 16 bytes.", nameof(nonce));
        }

        using var aes = Aes.Create();
        aes.Mode = CipherMode.ECB;
        aes.Padding = PaddingMode.None;
        aes.Key = key.ToArray();

        using var encryptor = aes.CreateEncryptor();

        var counter = nonce.ToArray();
        var keyStreamBlock = new byte[BlockSize];
        var plainText = new byte[cipherText.Length];
        var offset = 0;

        while (offset < cipherText.Length)
        {
            _ = encryptor.TransformBlock(counter, 0, BlockSize, keyStreamBlock, 0);

            var take = Math.Min(BlockSize, cipherText.Length - offset);

            for (var index = 0; index < take; index++)
            {
                plainText[offset + index] = (byte)(cipherText[offset + index] ^ keyStreamBlock[index]);
            }

            IncrementCounter(counter);
            offset += take;
        }

        return plainText;
    }

    private static void IncrementCounter(byte[] counter)
    {
        for (var index = counter.Length - 1; index >= 0; index--)
        {
            unchecked
            {
                counter[index]++;
            }

            if (counter[index] != 0)
            {
                return;
            }
        }
    }
}
