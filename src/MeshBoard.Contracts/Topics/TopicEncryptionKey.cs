using System.Globalization;

namespace MeshBoard.Contracts.Topics;

public static class TopicEncryptionKey
{
    public const string DefaultKeyBase64 = "AQ==";

    public static readonly byte[] DefaultKeyBytes =
    [
        0xd4, 0xf1, 0xbb, 0x3a, 0x20, 0x29, 0x07, 0x59,
        0xf0, 0xbc, 0xff, 0xab, 0xcf, 0x4e, 0x69, 0x01
    ];

    public static bool TryParse(string? value, out byte[] keyBytes)
    {
        keyBytes = [];

        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var trimmed = value.Trim();

        if (LooksLikeHex(trimmed) && TryParseHex(trimmed, out keyBytes))
        {
            return true;
        }

        if (TryParseBase64(trimmed, out keyBytes))
        {
            return true;
        }

        return TryParseHex(trimmed, out keyBytes);
    }

    public static string? NormalizeToBase64OrNull(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return TryParse(value, out var keyBytes)
            ? Convert.ToBase64String(keyBytes)
            : null;
    }

    private static bool TryParseBase64(string value, out byte[] keyBytes)
    {
        try
        {
            var decodedBytes = Convert.FromBase64String(NormalizeBase64Value(value));

            if (decodedBytes.Length == 1)
            {
                var pskIndex = decodedBytes[0];

                if (pskIndex == 0)
                {
                    keyBytes = [];
                    return false;
                }

                keyBytes = ExpandShortPsk(pskIndex);
                return true;
            }

            if (!IsValidLength(decodedBytes.Length))
            {
                keyBytes = [];
                return false;
            }

            keyBytes = decodedBytes;
            return true;
        }
        catch (FormatException)
        {
            keyBytes = [];
            return false;
        }
    }

    private static string NormalizeBase64Value(string value)
    {
        var normalized = value
            .Trim()
            .Replace('-', '+')
            .Replace('_', '/');

        var remainder = normalized.Length % 4;

        if (remainder == 0)
        {
            return normalized;
        }

        return normalized.PadRight(normalized.Length + (4 - remainder), '=');
    }

    private static bool TryParseHex(string value, out byte[] keyBytes)
    {
        var normalized = value
            .Replace("0x", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace(" ", string.Empty, StringComparison.Ordinal)
            .Replace("-", string.Empty, StringComparison.Ordinal)
            .Replace(":", string.Empty, StringComparison.Ordinal);

        if (normalized.Length == 0 || normalized.Length % 2 != 0)
        {
            keyBytes = [];
            return false;
        }

        var bytes = new byte[normalized.Length / 2];

        for (var index = 0; index < normalized.Length; index += 2)
        {
            if (!byte.TryParse(
                    normalized.AsSpan(index, 2),
                    NumberStyles.AllowHexSpecifier,
                    CultureInfo.InvariantCulture,
                    out bytes[index / 2]))
            {
                keyBytes = [];
                return false;
            }
        }

        if (!IsValidLength(bytes.Length))
        {
            keyBytes = [];
            return false;
        }

        keyBytes = bytes;
        return true;
    }

    private static bool LooksLikeHex(string value)
    {
        var normalized = value
            .Replace("0x", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace(" ", string.Empty, StringComparison.Ordinal)
            .Replace("-", string.Empty, StringComparison.Ordinal)
            .Replace(":", string.Empty, StringComparison.Ordinal);

        return normalized.Length > 0 &&
            normalized.Length % 2 == 0 &&
            normalized.All(Uri.IsHexDigit);
    }

    private static bool IsValidLength(int byteLength)
    {
        return byteLength is 16 or 24 or 32;
    }

    private static byte[] ExpandShortPsk(byte pskIndex)
    {
        var expanded = new byte[DefaultKeyBytes.Length];
        Buffer.BlockCopy(DefaultKeyBytes, 0, expanded, 0, DefaultKeyBytes.Length);

        unchecked
        {
            expanded[^1] = (byte)(expanded[^1] + pskIndex - 1);
        }

        return expanded;
    }
}
