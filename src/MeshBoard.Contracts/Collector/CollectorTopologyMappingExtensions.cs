using MeshBoard.Contracts.Nodes;

namespace MeshBoard.Contracts.Collector;

public static class CollectorTopologyMappingExtensions
{
    public static CollectorTopologyComponentSummary ToCollectorTopologyComponentSummary(
        this IReadOnlyCollection<string> nodeIds,
        int componentIndex,
        int linkCount)
    {
        ArgumentNullException.ThrowIfNull(nodeIds);

        return new CollectorTopologyComponentSummary
        {
            ComponentIndex = componentIndex,
            NodeCount = nodeIds.Count,
            LinkCount = linkCount,
            SampleNodeIds = nodeIds.Take(3).ToArray()
        };
    }

    public static CollectorTopologyNodeSummary ToCollectorTopologyNodeSummary(
        this NodeSummary node,
        int degree,
        int componentSize)
    {
        ArgumentNullException.ThrowIfNull(node);

        return new CollectorTopologyNodeSummary
        {
            NodeId = node.NodeId,
            ShortName = node.ShortName,
            LongName = node.LongName,
            Degree = degree,
            ComponentSize = componentSize
        };
    }
}
