using System.Collections.Concurrent;
using MeshBoard.Application.Abstractions.Meshtastic;
using MeshBoard.Contracts.Meshtastic;

namespace MeshBoard.RealtimeBridge;

internal sealed class ObservedInboundPacketLoggingSink : IMqttInboundMessageSink
{
    private readonly ConcurrentDictionary<string, long> _countsByWorkspace = new(StringComparer.Ordinal);
    private readonly ILogger<ObservedInboundPacketLoggingSink> _logger;

    public ObservedInboundPacketLoggingSink(ILogger<ObservedInboundPacketLoggingSink> logger)
    {
        _logger = logger;
    }

    public Task HandleAsync(MqttInboundMessage inboundMessage)
    {
        var workspaceId = string.IsNullOrWhiteSpace(inboundMessage.WorkspaceId)
            ? "(unknown-workspace)"
            : inboundMessage.WorkspaceId.Trim();

        var observedCount = _countsByWorkspace.AddOrUpdate(workspaceId, 1, static (_, current) => current + 1);

        if (observedCount == 1 || observedCount % 100 == 0)
        {
            _logger.LogInformation(
                "Observed raw MQTT packet #{ObservedCount} for workspace {WorkspaceId} from broker {BrokerServer} on topic {Topic} ({PayloadBytes} bytes)",
                observedCount,
                workspaceId,
                inboundMessage.BrokerServer,
                inboundMessage.Topic,
                inboundMessage.Payload.Length);
        }

        return Task.CompletedTask;
    }
}
