namespace MeshBoard.Infrastructure.Meshtastic.Runtime;

internal interface IBrokerRuntimeBootstrapService
{
    Task InitializeActiveWorkspacesAsync(CancellationToken cancellationToken = default);
}
