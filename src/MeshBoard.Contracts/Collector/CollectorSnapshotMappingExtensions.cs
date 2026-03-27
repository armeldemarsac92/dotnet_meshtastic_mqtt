using MeshBoard.Contracts.Nodes;

namespace MeshBoard.Contracts.Collector;

public static class CollectorSnapshotMappingExtensions
{
    public static CollectorMapSnapshot ToCollectorMapSnapshot(
        this CollectorMapQuery query,
        DateTimeOffset generatedAtUtc,
        IReadOnlyCollection<CollectorChannelSummary> channels,
        IReadOnlyCollection<NodeSummary> nodes,
        IReadOnlyCollection<CollectorMapLinkSummary> links)
    {
        ArgumentNullException.ThrowIfNull(query);

        return new CollectorMapSnapshot
        {
            GeneratedAtUtc = generatedAtUtc,
            ServerAddress = query.ServerAddress.NullIfWhiteSpace(),
            Region = query.Region.NullIfWhiteSpace(),
            ChannelName = query.ChannelName.NullIfWhiteSpace(),
            ActiveWithinHours = query.ActiveWithinHours,
            ServerCount = channels
                .Select(channel => channel.ServerAddress)
                .Distinct(StringComparer.Ordinal)
                .Count(),
            ChannelCount = channels.Count,
            NodeCount = nodes.Count,
            LinkCount = links.Count,
            Nodes = nodes,
            Links = links
        };
    }

    public static CollectorChannelPacketStatsSnapshot ToCollectorChannelPacketStatsSnapshot(
        this CollectorPacketStatsQuery query,
        DateTimeOffset generatedAtUtc,
        IReadOnlyCollection<CollectorChannelPacketHourlyRollup> rollups)
    {
        ArgumentNullException.ThrowIfNull(query);

        return new CollectorChannelPacketStatsSnapshot
        {
            GeneratedAtUtc = generatedAtUtc,
            ServerAddress = query.ServerAddress.NullIfWhiteSpace(),
            Region = query.Region.NullIfWhiteSpace(),
            ChannelName = query.ChannelName.NullIfWhiteSpace(),
            PacketType = query.PacketType.NullIfWhiteSpace(),
            LookbackHours = query.LookbackHours,
            RowCount = rollups.Count,
            Rollups = rollups
        };
    }

    public static CollectorNodePacketStatsSnapshot ToCollectorNodePacketStatsSnapshot(
        this CollectorPacketStatsQuery query,
        DateTimeOffset generatedAtUtc,
        IReadOnlyCollection<CollectorNodePacketHourlyRollup> rollups)
    {
        ArgumentNullException.ThrowIfNull(query);

        return new CollectorNodePacketStatsSnapshot
        {
            GeneratedAtUtc = generatedAtUtc,
            ServerAddress = query.ServerAddress.NullIfWhiteSpace(),
            Region = query.Region.NullIfWhiteSpace(),
            ChannelName = query.ChannelName.NullIfWhiteSpace(),
            NodeId = query.NodeId.NullIfWhiteSpace(),
            PacketType = query.PacketType.NullIfWhiteSpace(),
            LookbackHours = query.LookbackHours,
            RowCount = rollups.Count,
            Rollups = rollups
        };
    }

    public static CollectorNeighborLinkStatsSnapshot ToCollectorNeighborLinkStatsSnapshot(
        this CollectorNeighborLinkStatsQuery query,
        DateTimeOffset generatedAtUtc,
        IReadOnlyCollection<CollectorNeighborLinkHourlyRollup> rollups)
    {
        ArgumentNullException.ThrowIfNull(query);

        return new CollectorNeighborLinkStatsSnapshot
        {
            GeneratedAtUtc = generatedAtUtc,
            ServerAddress = query.ServerAddress.NullIfWhiteSpace(),
            Region = query.Region.NullIfWhiteSpace(),
            ChannelName = query.ChannelName.NullIfWhiteSpace(),
            SourceNodeId = query.SourceNodeId.NullIfWhiteSpace(),
            TargetNodeId = query.TargetNodeId.NullIfWhiteSpace(),
            LookbackHours = query.LookbackHours,
            RowCount = rollups.Count,
            Rollups = rollups
        };
    }

    public static CollectorTopologySnapshot ToCollectorTopologySnapshot(
        this CollectorTopologyQuery query,
        DateTimeOffset generatedAtUtc,
        IReadOnlyCollection<NodeSummary> nodes,
        IReadOnlyCollection<CollectorMapLinkSummary> links,
        int connectedComponentCount,
        int largestConnectedComponentSize,
        int isolatedNodeCount,
        int bridgeNodeCount,
        IReadOnlyCollection<CollectorTopologyComponentSummary> components,
        IReadOnlyCollection<CollectorTopologyNodeSummary> topDegreeNodes,
        IReadOnlyCollection<CollectorTopologyNodeSummary> bridgeNodes,
        IReadOnlyCollection<CollectorTopologyLinkSummary> strongestLinks)
    {
        ArgumentNullException.ThrowIfNull(query);

        return new CollectorTopologySnapshot
        {
            GeneratedAtUtc = generatedAtUtc,
            ServerAddress = query.ServerAddress.NullIfWhiteSpace(),
            Region = query.Region.NullIfWhiteSpace(),
            ChannelName = query.ChannelName.NullIfWhiteSpace(),
            ActiveWithinHours = query.ActiveWithinHours,
            NodeCount = nodes.Count,
            LinkCount = links.Count,
            ConnectedComponentCount = connectedComponentCount,
            LargestConnectedComponentSize = largestConnectedComponentSize,
            IsolatedNodeCount = isolatedNodeCount,
            BridgeNodeCount = bridgeNodeCount,
            Components = components,
            TopDegreeNodes = topDegreeNodes,
            BridgeNodes = bridgeNodes,
            StrongestLinks = strongestLinks
        };
    }

    public static CollectorOverviewSnapshot ToEmptyCollectorOverviewSnapshot(
        this CollectorOverviewQuery query,
        DateTimeOffset generatedAtUtc)
    {
        ArgumentNullException.ThrowIfNull(query);

        return new CollectorOverviewSnapshot
        {
            GeneratedAtUtc = generatedAtUtc,
            ServerAddress = query.ServerAddress.NullIfWhiteSpace(),
            Region = query.Region.NullIfWhiteSpace(),
            ChannelName = query.ChannelName.NullIfWhiteSpace(),
            ActiveWithinHours = query.ActiveWithinHours,
            LookbackHours = query.LookbackHours,
            ServerCount = 0,
            ChannelCount = 0,
            ActiveNodeCount = 0,
            ActiveLinkCount = 0,
            PacketCountInLookback = 0,
            NeighborObservationCountInLookback = 0
        };
    }

    public static CollectorOverviewSnapshot ToCollectorOverviewSnapshot(
        this CollectorOverviewQuery query,
        DateTimeOffset generatedAtUtc,
        IReadOnlyCollection<CollectorChannelSummary> channels,
        IReadOnlyCollection<CollectorOverviewServerSummary> serverSummaries)
    {
        ArgumentNullException.ThrowIfNull(query);

        return new CollectorOverviewSnapshot
        {
            GeneratedAtUtc = generatedAtUtc,
            ServerAddress = query.ServerAddress.NullIfWhiteSpace(),
            Region = query.Region.NullIfWhiteSpace(),
            ChannelName = query.ChannelName.NullIfWhiteSpace(),
            ActiveWithinHours = query.ActiveWithinHours,
            LookbackHours = query.LookbackHours,
            ServerCount = channels
                .Select(channel => channel.ServerAddress)
                .Distinct(StringComparer.Ordinal)
                .Count(),
            ChannelCount = channels.Count,
            ActiveNodeCount = serverSummaries.Sum(summary => summary.ActiveNodeCount),
            ActiveLinkCount = serverSummaries.Sum(summary => summary.ActiveLinkCount),
            PacketCountInLookback = serverSummaries.Sum(summary => summary.PacketCountInLookback),
            NeighborObservationCountInLookback = serverSummaries.Sum(summary => summary.NeighborObservationCountInLookback),
            Servers = serverSummaries
        };
    }

    public static CollectorOverviewServerSummary ToCollectorOverviewServerSummary(
        this IReadOnlyCollection<CollectorChannelSummary> channels,
        string serverAddress,
        IReadOnlyCollection<CollectorOverviewChannelSummary> selectedChannelSummaries,
        IReadOnlyCollection<NodeSummary> nodes,
        IReadOnlyCollection<CollectorMapLinkSummary> links,
        IReadOnlyCollection<CollectorOverviewPacketTypeSummary> packetTypeTotals,
        int neighborObservationCount)
    {
        return new CollectorOverviewServerSummary
        {
            ServerAddress = serverAddress,
            FirstObservedAtUtc = channels.Min(channel => channel.FirstObservedAtUtc),
            LastObservedAtUtc = channels.Max(channel => channel.LastObservedAtUtc),
            ChannelCount = channels.Count,
            ActiveNodeCount = nodes.Count,
            ActiveLinkCount = links.Count,
            PacketCountInLookback = packetTypeTotals.Sum(total => total.PacketCount),
            NeighborObservationCountInLookback = neighborObservationCount,
            Channels = selectedChannelSummaries
        };
    }

    public static CollectorOverviewChannelSummary ToCollectorOverviewChannelSummary(
        this CollectorChannelSummary channel,
        IReadOnlyCollection<NodeSummary> nodes,
        IReadOnlyCollection<CollectorMapLinkSummary> links,
        int connectedComponentCount,
        int largestConnectedComponentSize,
        int isolatedNodeCount,
        int bridgeNodeCount,
        int packetCountInLookback,
        IReadOnlyCollection<CollectorOverviewPacketTypeSummary> topPacketTypes,
        int neighborObservationCount)
    {
        ArgumentNullException.ThrowIfNull(channel);

        return new CollectorOverviewChannelSummary
        {
            Region = channel.Region,
            MeshVersion = channel.MeshVersion,
            ChannelName = channel.ChannelName,
            TopicPattern = channel.TopicPattern,
            FirstObservedAtUtc = channel.FirstObservedAtUtc,
            LastObservedAtUtc = channel.LastObservedAtUtc,
            ActiveNodeCount = nodes.Count,
            ActivePositionedNodeCount = nodes.Count(node => node.LastKnownLatitude.HasValue && node.LastKnownLongitude.HasValue),
            ActiveLinkCount = links.Count,
            ConnectedComponentCount = connectedComponentCount,
            LargestConnectedComponentSize = largestConnectedComponentSize,
            IsolatedNodeCount = isolatedNodeCount,
            BridgeNodeCount = bridgeNodeCount,
            PacketCountInLookback = packetCountInLookback,
            NeighborObservationCountInLookback = neighborObservationCount,
            TopPacketTypes = topPacketTypes
        };
    }

    public static CollectorTopologyLinkSummary ToCollectorTopologyLinkSummary(
        this CollectorMapLinkSummary link,
        NodeSummary? sourceNode,
        NodeSummary? targetNode,
        int observationCount,
        float? averageSnrDb,
        float? maxSnrDb,
        float? lastSnrDb)
    {
        ArgumentNullException.ThrowIfNull(link);

        return new CollectorTopologyLinkSummary
        {
            SourceNodeId = link.SourceNodeId,
            TargetNodeId = link.TargetNodeId,
            SourceShortName = sourceNode?.ShortName,
            SourceLongName = sourceNode?.LongName,
            TargetShortName = targetNode?.ShortName,
            TargetLongName = targetNode?.LongName,
            ObservationCount = observationCount,
            AverageSnrDb = averageSnrDb,
            MaxSnrDb = maxSnrDb,
            LastSnrDb = lastSnrDb
        };
    }
}
