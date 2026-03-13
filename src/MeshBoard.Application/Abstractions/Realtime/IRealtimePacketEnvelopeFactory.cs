using MeshBoard.Contracts.Meshtastic;
using MeshBoard.Contracts.Realtime;

namespace MeshBoard.Application.Abstractions.Realtime;

public interface IRealtimePacketEnvelopeFactory
{
    RealtimePacketEnvelope Create(MqttInboundMessage inboundMessage);
}
