using MeshBoard.Application.Abstractions.Meshtastic;
using MeshBoard.Contracts.Meshtastic;
using Microsoft.Extensions.Logging;

namespace MeshBoard.Infrastructure.Meshtastic.Hosted;

internal sealed class MeshtasticInboundQueueSink : IMqttInboundMessageSink
{
    private readonly MeshtasticInboundMessageQueue _inboundMessageQueue;
    private readonly ILogger<MeshtasticInboundQueueSink> _logger;

    public MeshtasticInboundQueueSink(
        MeshtasticInboundMessageQueue inboundMessageQueue,
        ILogger<MeshtasticInboundQueueSink> logger)
    {
        _inboundMessageQueue = inboundMessageQueue;
        _logger = logger;
    }

    public Task HandleAsync(MqttInboundMessage inboundMessage)
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
