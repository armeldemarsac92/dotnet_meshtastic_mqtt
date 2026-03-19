namespace MeshBoard.Contracts.Realtime;

public sealed class JsonWebKey
{
    public string Kty { get; set; } = "RSA";

    public string Use { get; set; } = "sig";

    public string Alg { get; set; } = "RS256";

    public string Kid { get; set; } = string.Empty;

    public string N { get; set; } = string.Empty;

    public string E { get; set; } = string.Empty;
}
