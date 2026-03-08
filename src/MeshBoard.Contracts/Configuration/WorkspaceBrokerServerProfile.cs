namespace MeshBoard.Contracts.Configuration;

public sealed class WorkspaceBrokerServerProfile
{
    public required string WorkspaceId { get; set; }

    public required BrokerServerProfile Profile { get; set; }
}
