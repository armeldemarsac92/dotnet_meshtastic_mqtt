using System.Security.Cryptography;
using MeshBoard.Application.Realtime;
using MeshBoard.Contracts.Configuration;
using Microsoft.Extensions.Options;

namespace MeshBoard.UnitTests;

public sealed class RealtimeJwksServiceTests
{
    [Fact]
    public void GetDocument_WhenSigningKeyUsesFilePath_ShouldBuildVerificationKey()
    {
        var keyFilePath = Path.GetTempFileName();

        try
        {
            using var rsa = RSA.Create(2048);
            File.WriteAllText(keyFilePath, rsa.ExportPkcs8PrivateKeyPem());

            var service = new RealtimeJwksService(
                Options.Create(
                    new RealtimeSessionOptions
                    {
                        KeyId = "meshboard-test-key",
                        SigningPrivateKeyPemFile = keyFilePath
                    }));

            var document = service.GetDocument();
            var key = Assert.Single(document.Keys);

            Assert.Equal("meshboard-test-key", key.Kid);
            Assert.False(string.IsNullOrWhiteSpace(key.N));
            Assert.False(string.IsNullOrWhiteSpace(key.E));
        }
        finally
        {
            if (File.Exists(keyFilePath))
            {
                File.Delete(keyFilePath);
            }
        }
    }
}
