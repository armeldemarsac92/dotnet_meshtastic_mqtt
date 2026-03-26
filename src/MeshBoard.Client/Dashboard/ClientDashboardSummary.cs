namespace MeshBoard.Client.Dashboard;

public sealed record ClientDashboardSummary
{
    public static readonly IReadOnlyList<ClientDashboardChannelSummary> EmptyChannels = Array.Empty<ClientDashboardChannelSummary>();

    public static readonly IReadOnlyList<ClientDashboardNodeSummary> EmptyNodes = Array.Empty<ClientDashboardNodeSummary>();

    public static readonly IReadOnlyList<ClientDashboardMessageSummary> EmptyMessages = Array.Empty<ClientDashboardMessageSummary>();

    public long RawPacketCount { get; init; }

    public long DecryptedMessageCount { get; init; }

    public int ObservedNodeCount { get; init; }

    public int LocatedNodeCount { get; init; }

    public int ObservedChannelCount { get; init; }

    public DateTimeOffset? LastActivityAtUtc { get; init; }

    public int SuccessfulDecryptCount { get; init; }

    public int NoMatchingKeyCount { get; init; }

    public int DecryptFailureCount { get; init; }

    public IReadOnlyList<ClientDashboardChannelSummary> ActiveChannels { get; init; } = EmptyChannels;

    public IReadOnlyList<ClientDashboardNodeSummary> ActiveNodes { get; init; } = EmptyNodes;

    public IReadOnlyList<ClientDashboardMessageSummary> RecentMessages { get; init; } = EmptyMessages;
}

public sealed record ClientDashboardChannelSummary
{
    public string ChannelKey { get; init; } = string.Empty;

    public string LastPacketType { get; init; } = string.Empty;

    public int ObservedPacketCount { get; init; }

    public int DistinctNodeCount { get; init; }

    public DateTimeOffset? LastObservedAtUtc { get; init; }
}

public sealed record ClientDashboardNodeSummary
{
    public string NodeId { get; init; } = string.Empty;

    public string DisplayName { get; init; } = string.Empty;

    public string? Channel { get; init; }

    public int ObservedPacketCount { get; init; }

    public bool HasLocation { get; init; }

    public bool HasTelemetry { get; init; }

    public DateTimeOffset? LastHeardAtUtc { get; init; }
}

public sealed record ClientDashboardMessageSummary
{
    public string PacketType { get; init; } = string.Empty;

    public string PayloadPreview { get; init; } = string.Empty;

    public string SourceTopic { get; init; } = string.Empty;

    public DateTimeOffset ReceivedAtUtc { get; init; }
}
