using MeshBoard.Contracts.Meshtastic;

namespace MeshBoard.Application.Abstractions.Meshtastic;

public interface IWorkspaceBrokerSessionManager
{
    bool IsConnected(string workspaceId);

    string? GetLastStatusMessage(string workspaceId);

    IReadOnlyCollection<string> GetTopicFilters(string workspaceId);

    event Func<MqttInboundMessage, Task>? MessageReceived;

    Task ResetRuntimeAsync(string workspaceId, CancellationToken cancellationToken = default);

    Task ConnectAsync(string workspaceId, CancellationToken cancellationToken = default);

    Task DisconnectAsync(string workspaceId, CancellationToken cancellationToken = default);

    Task DisconnectAllAsync(CancellationToken cancellationToken = default);

    Task PublishAsync(string workspaceId, string topic, string payload, CancellationToken cancellationToken = default);

    Task SubscribeAsync(string workspaceId, string topicFilter, CancellationToken cancellationToken = default);

    Task UnsubscribeAsync(string workspaceId, string topicFilter, CancellationToken cancellationToken = default);
}
