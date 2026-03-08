using MeshBoard.Application.Abstractions.Meshtastic;
using MeshBoard.Application.Abstractions.Persistence;
using MeshBoard.Contracts.Meshtastic;

namespace MeshBoard.Infrastructure.Meshtastic.Runtime;

internal sealed class QueuedBrokerRuntimeCommandService : IBrokerRuntimeCommandService
{
    private readonly IBrokerRuntimeCommandRepository _brokerRuntimeCommandRepository;
    private readonly TimeProvider _timeProvider;

    public QueuedBrokerRuntimeCommandService(
        IBrokerRuntimeCommandRepository brokerRuntimeCommandRepository,
        TimeProvider timeProvider)
    {
        _brokerRuntimeCommandRepository = brokerRuntimeCommandRepository;
        _timeProvider = timeProvider;
    }

    public Task EnsureConnectedAsync(string workspaceId, CancellationToken cancellationToken = default)
    {
        return EnqueueAsync(
            workspaceId,
            BrokerRuntimeCommandType.EnsureConnected,
            cancellationToken: cancellationToken);
    }

    public Task ReconcileActiveProfileAsync(string workspaceId, CancellationToken cancellationToken = default)
    {
        return EnqueueAsync(
            workspaceId,
            BrokerRuntimeCommandType.ReconcileActiveProfile,
            cancellationToken: cancellationToken);
    }

    public Task ResetAndReconnectActiveProfileAsync(string workspaceId, CancellationToken cancellationToken = default)
    {
        return EnqueueAsync(
            workspaceId,
            BrokerRuntimeCommandType.ResetAndReconnectActiveProfile,
            cancellationToken: cancellationToken);
    }

    public Task PublishAsync(
        string workspaceId,
        string topic,
        string payload,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(topic);
        ArgumentException.ThrowIfNullOrWhiteSpace(payload);

        return EnqueueAsync(
            workspaceId,
            BrokerRuntimeCommandType.Publish,
            topic,
            payload,
            cancellationToken: cancellationToken);
    }

    public Task SubscribeEphemeralAsync(
        string workspaceId,
        string topicFilter,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(topicFilter);

        return EnqueueAsync(
            workspaceId,
            BrokerRuntimeCommandType.SubscribeEphemeral,
            topicFilter: topicFilter,
            cancellationToken: cancellationToken);
    }

    public Task UnsubscribeEphemeralAsync(
        string workspaceId,
        string topicFilter,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(topicFilter);

        return EnqueueAsync(
            workspaceId,
            BrokerRuntimeCommandType.UnsubscribeEphemeral,
            topicFilter: topicFilter,
            cancellationToken: cancellationToken);
    }

    private Task EnqueueAsync(
        string workspaceId,
        BrokerRuntimeCommandType commandType,
        string? topic = null,
        string? payload = null,
        string? topicFilter = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceId);

        var utcNow = _timeProvider.GetUtcNow();

        return _brokerRuntimeCommandRepository.EnqueueAsync(
            new BrokerRuntimeCommand
            {
                Id = Guid.NewGuid(),
                WorkspaceId = workspaceId,
                CommandType = commandType,
                Topic = topic,
                Payload = payload,
                TopicFilter = topicFilter,
                AttemptCount = 0,
                CreatedAtUtc = utcNow,
                AvailableAtUtc = utcNow
            },
            cancellationToken);
    }
}
