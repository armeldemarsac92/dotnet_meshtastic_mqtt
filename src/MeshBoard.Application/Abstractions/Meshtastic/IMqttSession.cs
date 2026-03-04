using MeshBoard.Contracts.Meshtastic;

namespace MeshBoard.Application.Abstractions.Meshtastic;

public interface IMqttSession
{
    bool IsConnected { get; }

    string? LastStatusMessage { get; }

    IReadOnlyCollection<string> TopicFilters { get; }

    event Func<bool, Task>? ConnectionStateChanged;

    event Func<MqttInboundMessage, Task>? MessageReceived;

    Task ConnectAsync(CancellationToken cancellationToken = default);

    Task DisconnectAsync(CancellationToken cancellationToken = default);

    Task PublishAsync(string topic, string payload, CancellationToken cancellationToken = default);

    Task SubscribeAsync(string topicFilter, CancellationToken cancellationToken = default);

    Task UnsubscribeAsync(string topicFilter, CancellationToken cancellationToken = default);
}
