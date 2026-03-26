using MeshBoard.Contracts.Favorites;
using MeshBoard.Contracts.Messages;
using MeshBoard.Contracts.Meshtastic;

namespace MeshBoard.Contracts.Dashboard;

public sealed class HomeDashboardSnapshot
{
    public BrokerStatus BrokerStatus { get; set; } = new();

    public IReadOnlyCollection<FavoriteNode> FavoriteNodes { get; set; } = [];

    public IReadOnlyCollection<MessageSummary> Messages { get; set; } = [];

    public int ObservedNodeCount { get; set; }
}
