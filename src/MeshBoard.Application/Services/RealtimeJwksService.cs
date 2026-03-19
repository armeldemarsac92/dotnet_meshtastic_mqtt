using System.Security.Cryptography;
using MeshBoard.Contracts.Configuration;
using MeshBoard.Contracts.Realtime;
using Microsoft.Extensions.Options;

namespace MeshBoard.Application.Services;

public interface IRealtimeJwksService
{
    JsonWebKeyDocument GetDocument();
}

public sealed class RealtimeJwksService : IRealtimeJwksService
{
    private readonly Lazy<JsonWebKeyDocument> _document;

    public RealtimeJwksService(IOptions<RealtimeSessionOptions> options)
    {
        var resolvedOptions = options.Value;
        var signingPrivateKeyPem = RealtimeSigningKeyMaterialResolver.ResolvePrivateKeyPem(resolvedOptions);
        _document = new Lazy<JsonWebKeyDocument>(() => BuildDocument(resolvedOptions, signingPrivateKeyPem));
    }

    public JsonWebKeyDocument GetDocument()
    {
        return _document.Value;
    }

    private static JsonWebKeyDocument BuildDocument(RealtimeSessionOptions options, string signingPrivateKeyPem)
    {
        if (string.IsNullOrWhiteSpace(options.KeyId))
        {
            throw new InvalidOperationException("RealtimeSession:KeyId is required.");
        }

        using var rsa = RSA.Create();
        rsa.ImportFromPem(signingPrivateKeyPem.AsSpan());

        var parameters = rsa.ExportParameters(false);
        if (parameters.Modulus is null || parameters.Exponent is null)
        {
            throw new InvalidOperationException("Unable to derive the realtime verification key.");
        }

        return new JsonWebKeyDocument
        {
            Keys =
            [
                new JsonWebKey
                {
                    Kid = options.KeyId,
                    N = Base64UrlEncode(parameters.Modulus),
                    E = Base64UrlEncode(parameters.Exponent)
                }
            ]
        };
    }

    private static string Base64UrlEncode(byte[] value)
    {
        return Convert.ToBase64String(value)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }
}
