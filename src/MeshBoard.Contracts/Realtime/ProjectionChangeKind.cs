namespace MeshBoard.Contracts.Realtime;

public enum ProjectionChangeKind
{
    MessageAdded = 0,
    NodeUpdated = 1,
    ChannelSummaryUpdated = 2,
    RuntimeStatusChanged = 3,
    RuntimeCommandChanged = 4,
    FavoriteNodesChanged = 5
}
