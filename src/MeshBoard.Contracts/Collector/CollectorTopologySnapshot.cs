namespace MeshBoard.Contracts.Collector;

public sealed class CollectorTopologySnapshot
{
    public DateTimeOffset GeneratedAtUtc { get; set; }

    public string WorkspaceId { get; set; } = string.Empty;

    public string? ServerAddress { get; set; }

    public string? Region { get; set; }

    public string? ChannelName { get; set; }

    public int ActiveWithinHours { get; set; }

    public int NodeCount { get; set; }

    public int LinkCount { get; set; }

    public int ConnectedComponentCount { get; set; }

    public int LargestConnectedComponentSize { get; set; }

    public int IsolatedNodeCount { get; set; }

    public int BridgeNodeCount { get; set; }

    public IReadOnlyCollection<CollectorTopologyComponentSummary> Components { get; set; } = [];

    public IReadOnlyCollection<CollectorTopologyNodeSummary> TopDegreeNodes { get; set; } = [];

    public IReadOnlyCollection<CollectorTopologyNodeSummary> BridgeNodes { get; set; } = [];

    public IReadOnlyCollection<CollectorTopologyLinkSummary> StrongestLinks { get; set; } = [];
}
