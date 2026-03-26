namespace MeshBoard.Client.Channels;

public sealed record ChannelProjectionEnvelope
{
    public string Id { get; init; } = string.Empty;

    public string BrokerServer { get; init; } = string.Empty;

    public string Region { get; init; } = string.Empty;

    public string ChannelName { get; init; } = string.Empty;

    public string ChannelKey { get; init; } = string.Empty;

    public string LatestSourceTopic { get; init; } = string.Empty;

    public DateTimeOffset? LastObservedAtUtc { get; init; }

    public string LastPacketType { get; init; } = string.Empty;

    public string LastPayloadPreview { get; init; } = string.Empty;

    public int ObservedPacketCount { get; init; }

    public IReadOnlyList<string> ObservedNodeIds { get; init; } = Array.Empty<string>();

    public int DistinctNodeCount => ObservedNodeIds.Count;
}
