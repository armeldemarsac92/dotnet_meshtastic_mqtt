using MeshBoard.Contracts.Meshtastic;

namespace MeshBoard.Collector.Ingress.Services;

public interface IRawPacketPublisherService
{
    Task PublishAsync(MqttInboundMessage inboundMessage, CancellationToken cancellationToken = default);
}
