using MeshBoard.Contracts.Configuration;

namespace MeshBoard.Application.Services;

internal static class RealtimeSigningKeyMaterialResolver
{
    public static string ResolvePrivateKeyPem(RealtimeSessionOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (!string.IsNullOrWhiteSpace(options.SigningPrivateKeyPem))
        {
            return options.SigningPrivateKeyPem;
        }

        if (string.IsNullOrWhiteSpace(options.SigningPrivateKeyPemFile))
        {
            throw new InvalidOperationException(
                "RealtimeSession:SigningPrivateKeyPem or RealtimeSession:SigningPrivateKeyPemFile is required.");
        }

        var filePath = options.SigningPrivateKeyPemFile.Trim();
        if (!Path.IsPathRooted(filePath))
        {
            filePath = Path.GetFullPath(filePath);
        }

        if (!File.Exists(filePath))
        {
            throw new InvalidOperationException(
                $"RealtimeSession:SigningPrivateKeyPemFile does not exist: {filePath}");
        }

        var pem = File.ReadAllText(filePath).Trim();
        if (string.IsNullOrWhiteSpace(pem))
        {
            throw new InvalidOperationException(
                $"RealtimeSession:SigningPrivateKeyPemFile is empty: {filePath}");
        }

        return pem;
    }
}
