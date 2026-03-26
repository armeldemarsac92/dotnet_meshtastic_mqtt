namespace MeshBoard.Contracts.Realtime;

public sealed class RealtimeSessionResponse
{
    public string BrokerUrl { get; set; } = string.Empty;

    public string ClientId { get; set; } = string.Empty;

    public string Token { get; set; } = string.Empty;

    public DateTimeOffset ExpiresAtUtc { get; set; }

    public List<string> AllowedTopicPatterns { get; set; } = [];
}
