using MeshBoard.Application.Abstractions.Meshtastic;
using MeshBoard.Application.Abstractions.Realtime;
using MeshBoard.Contracts.Meshtastic;

namespace MeshBoard.RealtimeBridge;

internal sealed class RealtimePacketPublishingSink : IMqttInboundMessageSink
{
    private readonly IRealtimePacketEnvelopeFactory _envelopeFactory;
    private readonly IRealtimePacketPublisher _packetPublisher;

    public RealtimePacketPublishingSink(
        IRealtimePacketEnvelopeFactory envelopeFactory,
        IRealtimePacketPublisher packetPublisher)
    {
        _envelopeFactory = envelopeFactory;
        _packetPublisher = packetPublisher;
    }

    public Task HandleAsync(MqttInboundMessage inboundMessage)
    {
        var envelope = _envelopeFactory.Create(inboundMessage);
        return _packetPublisher.PublishAsync(envelope);
    }
}
