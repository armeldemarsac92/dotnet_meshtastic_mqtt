using MeshBoard.Contracts.Meshtastic;

namespace MeshBoard.Application.Abstractions.Meshtastic;

public interface IBrokerRuntimeRegistry
{
    BrokerRuntimeSnapshot GetSnapshot(string workspaceId);

    void UpdateSnapshot(string workspaceId, BrokerRuntimeSnapshot snapshot);

    RuntimePipelineSnapshot GetPipelineSnapshot(string workspaceId);

    void UpdatePipelineSnapshot(string workspaceId, RuntimePipelineSnapshot snapshot);
}
