namespace MeshBoard.Application.Abstractions.Meshtastic;

public interface IBrokerRuntimeCommandService
{
    Task EnsureConnectedAsync(string workspaceId, CancellationToken cancellationToken = default);

    Task ReconcileActiveProfileAsync(string workspaceId, CancellationToken cancellationToken = default);

    Task ResetAndReconnectActiveProfileAsync(string workspaceId, CancellationToken cancellationToken = default);

    Task PublishAsync(
        string workspaceId,
        string topic,
        string payload,
        CancellationToken cancellationToken = default);

    Task SubscribeEphemeralAsync(
        string workspaceId,
        string topicFilter,
        CancellationToken cancellationToken = default);

    Task UnsubscribeEphemeralAsync(
        string workspaceId,
        string topicFilter,
        CancellationToken cancellationToken = default);
}
