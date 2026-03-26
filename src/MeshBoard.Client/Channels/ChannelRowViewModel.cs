using MeshBoard.Contracts.Collector;

namespace MeshBoard.Client.Channels;

public sealed record ChannelRowViewModel
{
    public string ChannelKey { get; init; } = string.Empty;
    public string Region { get; init; } = string.Empty;
    public string ChannelName { get; init; } = string.Empty;
    public string ServerAddress { get; init; } = string.Empty;
    public DateTimeOffset? LastObservedAtUtc { get; init; }
    public int NodeCount { get; init; }
    public int PacketCount { get; init; }
    public int NeighborLinkCount { get; init; }
    // Live-only (null for history)
    public string? LastPacketType { get; init; }
    public string? LatestSourceTopic { get; init; }
    public string? LastPayloadPreview { get; init; }

    public static ChannelRowViewModel From(ChannelProjectionEnvelope e) => new()
    {
        ChannelKey = e.ChannelKey,
        Region = e.Region,
        ChannelName = e.ChannelName,
        ServerAddress = e.BrokerServer,
        LastObservedAtUtc = e.LastObservedAtUtc,
        NodeCount = e.DistinctNodeCount,
        PacketCount = e.ObservedPacketCount,
        NeighborLinkCount = 0,
        LastPacketType = string.IsNullOrWhiteSpace(e.LastPacketType) ? null : e.LastPacketType,
        LatestSourceTopic = string.IsNullOrWhiteSpace(e.LatestSourceTopic) ? null : e.LatestSourceTopic,
        LastPayloadPreview = string.IsNullOrWhiteSpace(e.LastPayloadPreview) ? null : e.LastPayloadPreview,
    };

    public static ChannelRowViewModel From(CollectorChannelSummary s) => new()
    {
        ChannelKey = $"{s.Region}/{s.ChannelName}",
        Region = s.Region,
        ChannelName = s.ChannelName,
        ServerAddress = s.ServerAddress,
        LastObservedAtUtc = s.LastObservedAtUtc,
        NodeCount = s.NodeCount,
        PacketCount = s.MessageCount,
        NeighborLinkCount = s.NeighborLinkCount,
        LastPacketType = null,
        LatestSourceTopic = string.IsNullOrWhiteSpace(s.TopicPattern) ? null : s.TopicPattern,
        LastPayloadPreview = null,
    };
}
