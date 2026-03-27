namespace MeshBoard.Contracts.Realtime;

public static class RealtimeSessionMappingExtensions
{
    public static RealtimeSessionResponse ToRealtimeSessionResponse(
        this RealtimeTopicAccessPolicy accessPolicy,
        Uri brokerUri,
        string clientId,
        string token,
        DateTimeOffset expiresAtUtc)
    {
        ArgumentNullException.ThrowIfNull(accessPolicy);
        ArgumentNullException.ThrowIfNull(brokerUri);

        return new RealtimeSessionResponse
        {
            BrokerUrl = brokerUri.ToString(),
            ClientId = clientId,
            Token = token,
            ExpiresAtUtc = expiresAtUtc,
            AllowedTopicPatterns = accessPolicy.SubscribeTopicPatterns
        };
    }
}
