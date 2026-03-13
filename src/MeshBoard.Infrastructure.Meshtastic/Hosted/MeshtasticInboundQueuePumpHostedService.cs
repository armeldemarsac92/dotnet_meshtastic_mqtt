using MeshBoard.Application.Abstractions.Meshtastic;
using MeshBoard.Contracts.Meshtastic;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace MeshBoard.Infrastructure.Meshtastic.Hosted;

internal sealed class MeshtasticInboundQueuePumpHostedService : IHostedService
{
    private readonly MeshtasticInboundMessageQueue _inboundMessageQueue;
    private readonly IWorkspaceBrokerSessionManager _brokerSessionManager;
    private readonly ILogger<MeshtasticInboundQueuePumpHostedService> _logger;

    public MeshtasticInboundQueuePumpHostedService(
        MeshtasticInboundMessageQueue inboundMessageQueue,
        IWorkspaceBrokerSessionManager brokerSessionManager,
        ILogger<MeshtasticInboundQueuePumpHostedService> logger)
    {
        _inboundMessageQueue = inboundMessageQueue;
        _brokerSessionManager = brokerSessionManager;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _brokerSessionManager.MessageReceived += OnMessageReceivedAsync;
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _brokerSessionManager.MessageReceived -= OnMessageReceivedAsync;
        _inboundMessageQueue.Complete();
        return Task.CompletedTask;
    }

    private Task OnMessageReceivedAsync(MqttInboundMessage inboundMessage)
    {
        if (!_inboundMessageQueue.TryEnqueue(inboundMessage))
        {
            var snapshot = _inboundMessageQueue.GetSnapshot(inboundMessage.WorkspaceId);
            var shouldLog = snapshot.DroppedCount == 1 || snapshot.DroppedCount % 100 == 0;

            if (shouldLog)
            {
                _logger.LogWarning(
                    "Dropping Meshtastic inbound message for workspace {WorkspaceId} because the inbound queue is full. Depth: {QueueDepth}; Dropped: {DroppedCount}",
                    inboundMessage.WorkspaceId,
                    snapshot.CurrentDepth,
                    snapshot.DroppedCount);
            }
        }

        return Task.CompletedTask;
    }
}
