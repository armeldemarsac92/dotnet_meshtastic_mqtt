using MeshBoard.Application.Abstractions.Realtime;
using MeshBoard.Contracts.Meshtastic;
using MeshBoard.Contracts.Realtime;

namespace MeshBoard.Application.Realtime;

public sealed class RealtimePacketEnvelopeFactory : IRealtimePacketEnvelopeFactory
{
    public RealtimePacketEnvelope Create(MqttInboundMessage inboundMessage)
    {
        ArgumentNullException.ThrowIfNull(inboundMessage);

        return new RealtimePacketEnvelope
        {
            WorkspaceId = NormalizeText(inboundMessage.WorkspaceId),
            BrokerServer = NormalizeText(inboundMessage.BrokerServer),
            Topic = NormalizeText(inboundMessage.Topic),
            Payload = inboundMessage.Payload.ToArray(),
            ReceivedAtUtc = inboundMessage.ReceivedAtUtc
        };
    }

    private static string NormalizeText(string? value) => value?.Trim() ?? string.Empty;
}
