namespace MeshBoard.Contracts.Collector;

public sealed class CollectorMapLinkSummary
{
    public string SourceNodeId { get; set; } = string.Empty;

    public string TargetNodeId { get; set; } = string.Empty;

    public float? SnrDb { get; set; }

    public DateTimeOffset LastSeenAtUtc { get; set; }

    public string ServerAddress { get; set; } = string.Empty;

    public string Region { get; set; } = string.Empty;

    public string MeshVersion { get; set; } = string.Empty;

    public string ChannelName { get; set; } = string.Empty;
}
