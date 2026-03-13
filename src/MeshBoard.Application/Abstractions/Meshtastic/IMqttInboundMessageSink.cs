using MeshBoard.Contracts.Meshtastic;

namespace MeshBoard.Application.Abstractions.Meshtastic;

public interface IMqttInboundMessageSink
{
    Task HandleAsync(MqttInboundMessage inboundMessage);
}
