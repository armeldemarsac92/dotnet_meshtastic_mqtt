namespace MeshBoard.Contracts.Configuration;

public sealed class RealtimeSessionOptions
{
    public const string SectionName = "RealtimeSession";

    public string BrokerUrl { get; set; } = "wss://localhost:8084/mqtt";

    public string Issuer { get; set; } = "meshboard-api";

    public string Audience { get; set; } = "meshboard-realtime";

    public string KeyId { get; set; } = "meshboard-realtime-v1";

    public string ClientIdPrefix { get; set; } = "mb";

    public int TokenLifetimeMinutes { get; set; } = 5;

    public string SigningPrivateKeyPem { get; set; } = string.Empty;

    public string SigningPrivateKeyPemFile { get; set; } = string.Empty;
}
