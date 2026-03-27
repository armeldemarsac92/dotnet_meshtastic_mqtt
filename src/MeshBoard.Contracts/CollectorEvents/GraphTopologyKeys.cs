namespace MeshBoard.Contracts.CollectorEvents;

public static class GraphTopologyKeys
{
    /// <summary>
    /// Returns (sourceNodeId, targetNodeId) in lexicographic order so that
    /// RADIO_LINK merges are always keyed the same way regardless of observation direction.
    /// </summary>
    public static (string SourceNodeId, string TargetNodeId) CanonicalNodePair(string sourceNodeId, string targetNodeId)
    {
        return string.CompareOrdinal(sourceNodeId, targetNodeId) <= 0
            ? (sourceNodeId, targetNodeId)
            : (targetNodeId, sourceNodeId);
    }
}
