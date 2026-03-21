using MeshBoard.Contracts.Configuration;

namespace MeshBoard.Client.Dashboard;

public sealed record ClientDashboardPreferenceSummary
{
    public static readonly ClientDashboardPreferenceSummary Empty = new();

    public SavedBrokerServerProfile? ActiveBroker { get; init; }

    public int SavedBrokerCount { get; init; }

    public int FavoriteCount { get; init; }

    public bool IsLoaded { get; init; }
}
