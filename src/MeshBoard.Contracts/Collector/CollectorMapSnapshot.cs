using MeshBoard.Contracts.Nodes;

namespace MeshBoard.Contracts.Collector;

public sealed class CollectorMapSnapshot
{
    public DateTimeOffset GeneratedAtUtc { get; set; }

    public string WorkspaceId { get; set; } = string.Empty;

    public string? ServerAddress { get; set; }

    public string? Region { get; set; }

    public string? ChannelName { get; set; }

    public int ActiveWithinHours { get; set; }

    public int ServerCount { get; set; }

    public int ChannelCount { get; set; }

    public int NodeCount { get; set; }

    public int LinkCount { get; set; }

    public IReadOnlyCollection<NodeSummary> Nodes { get; set; } = [];

    public IReadOnlyCollection<CollectorMapLinkSummary> Links { get; set; } = [];
}
