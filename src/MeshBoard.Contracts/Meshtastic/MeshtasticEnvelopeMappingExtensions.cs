using MeshBoard.Contracts.Messages;
using MeshBoard.Contracts.Nodes;

namespace MeshBoard.Contracts.Meshtastic;

public static class MeshtasticEnvelopeMappingExtensions
{
    public static SaveObservedMessageRequest ToSaveObservedMessageRequest(
        this MeshtasticEnvelope envelope,
        string messageKey)
    {
        ArgumentNullException.ThrowIfNull(envelope);

        return new SaveObservedMessageRequest
        {
            BrokerServer = envelope.BrokerServer,
            Topic = envelope.Topic,
            PacketType = envelope.PacketType,
            MessageKey = messageKey,
            FromNodeId = envelope.FromNodeId ?? "unknown",
            ToNodeId = envelope.ToNodeId,
            PayloadPreview = envelope.PayloadPreview,
            IsPrivate = envelope.IsPrivate,
            ReceivedAtUtc = envelope.ReceivedAtUtc
        };
    }

    public static UpsertObservedNodeRequest ToUpsertObservedNodeRequest(this MeshtasticEnvelope envelope)
    {
        ArgumentNullException.ThrowIfNull(envelope);

        return new UpsertObservedNodeRequest
        {
            NodeId = envelope.FromNodeId ?? throw new ArgumentException("Envelope must contain a source node id.", nameof(envelope)),
            BrokerServer = envelope.BrokerServer,
            ShortName = envelope.ShortName,
            LongName = envelope.LongName,
            LastHeardAtUtc = envelope.ReceivedAtUtc,
            LastHeardChannel = envelope.LastHeardChannel,
            LastTextMessageAtUtc = envelope.PacketType == "Text Message"
                ? envelope.ReceivedAtUtc
                : null,
            LastKnownLatitude = envelope.Latitude,
            LastKnownLongitude = envelope.Longitude,
            BatteryLevelPercent = envelope.BatteryLevelPercent,
            Voltage = envelope.Voltage,
            ChannelUtilization = envelope.ChannelUtilization,
            AirUtilTx = envelope.AirUtilTx,
            UptimeSeconds = envelope.UptimeSeconds,
            TemperatureCelsius = envelope.TemperatureCelsius,
            RelativeHumidity = envelope.RelativeHumidity,
            BarometricPressure = envelope.BarometricPressure
        };
    }
}
