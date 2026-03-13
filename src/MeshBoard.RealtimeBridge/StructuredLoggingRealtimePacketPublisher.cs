using System.Collections.Concurrent;
using MeshBoard.Contracts.Realtime;

namespace MeshBoard.RealtimeBridge;

internal sealed class StructuredLoggingRealtimePacketPublisher : IRealtimePacketPublisher
{
    private readonly ConcurrentDictionary<string, long> _countsByWorkspace = new(StringComparer.Ordinal);
    private readonly ILogger<StructuredLoggingRealtimePacketPublisher> _logger;

    public StructuredLoggingRealtimePacketPublisher(ILogger<StructuredLoggingRealtimePacketPublisher> logger)
    {
        _logger = logger;
    }

    public Task PublishAsync(RealtimePacketEnvelope envelope)
    {
        var workspaceId = string.IsNullOrWhiteSpace(envelope.WorkspaceId)
            ? "(unknown-workspace)"
            : envelope.WorkspaceId.Trim();

        var observedCount = _countsByWorkspace.AddOrUpdate(workspaceId, 1, static (_, current) => current + 1);

        if (observedCount == 1 || observedCount % 100 == 0)
        {
            _logger.LogInformation(
                "Prepared realtime packet #{ObservedCount} for workspace {WorkspaceId} from broker {BrokerServer} on topic {Topic} ({PayloadBytes} bytes)",
                observedCount,
                workspaceId,
                envelope.BrokerServer,
                envelope.Topic,
                envelope.Payload.Length);
        }

        return Task.CompletedTask;
    }
}
