namespace MeshBoard.Contracts.CollectorEvents.Normalized;

public sealed class LinkObserved : CollectorEventMetadata
{
    public string TopicPattern { get; init; } = string.Empty;

    public string ChannelKey { get; init; } = string.Empty;

    public string SourceNodeId { get; init; } = string.Empty;

    public string TargetNodeId { get; init; } = string.Empty;

    public DateTimeOffset ObservedAtUtc { get; init; }

    public float? SnrDb { get; init; }

    public CollectorLinkOrigin LinkOrigin { get; init; }
}
