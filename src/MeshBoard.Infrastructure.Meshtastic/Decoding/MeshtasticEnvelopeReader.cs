using System.Globalization;
using System.Text;
using Google.Protobuf;
using MeshBoard.Application.Abstractions.Meshtastic;
using MeshBoard.Contracts.Meshtastic;
using MeshBoard.Infrastructure.Meshtastic.Protobuf;
using Microsoft.Extensions.Logging;

namespace MeshBoard.Infrastructure.Meshtastic.Decoding;

internal sealed class MeshtasticEnvelopeReader : IMeshtasticEnvelopeReader
{
    private const uint BroadcastNodeNumber = uint.MaxValue;
    private readonly ILogger<MeshtasticEnvelopeReader> _logger;

    public MeshtasticEnvelopeReader(ILogger<MeshtasticEnvelopeReader> logger)
    {
        _logger = logger;
    }

    public Task<MeshtasticEnvelope?> Read(
        string topic,
        byte[] payload,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return Task.FromResult(ReadCore(topic, payload));
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogDebug(
                exception,
                "Skipping Meshtastic payload that could not be decoded for topic: {Topic}",
                topic);

            return Task.FromResult<MeshtasticEnvelope?>(null);
        }
    }

    private MeshtasticEnvelope? ReadCore(string topic, byte[] payload)
    {
        if (TryParseServiceEnvelope(payload, out var serviceEnvelope) &&
            serviceEnvelope is not null &&
            !serviceEnvelope.Packet.IsEmpty &&
            TryParseMeshPacket(serviceEnvelope.Packet.ToByteArray(), out var servicePacket) &&
            servicePacket is not null)
        {
            return MapEnvelope(topic, servicePacket, payload.Length);
        }

        if (TryParseMeshPacket(payload, out var directPacket) && directPacket is not null)
        {
            return MapEnvelope(topic, directPacket, payload.Length);
        }

        return null;
    }

    private MeshtasticEnvelope MapEnvelope(string topic, MeshPacket packet, int payloadLength)
    {
        var envelope = new MeshtasticEnvelope
        {
            Topic = topic,
            PacketId = packet.Id == 0 ? null : packet.Id,
            FromNodeId = FormatNodeId(packet.From) ?? TryExtractNodeIdFromTopic(topic),
            ToNodeId = ResolveTargetNodeId(packet.To),
            IsPrivate = IsPrivate(packet.To),
            ReceivedAtUtc = ResolveReceivedAtUtc(packet.RxTime)
        };

        if (packet.PayloadVariantCase != MeshPacket.PayloadVariantOneofCase.Decoded)
        {
            envelope.PacketType = packet.PayloadVariantCase == MeshPacket.PayloadVariantOneofCase.Encrypted
                ? "Encrypted Packet"
                : "Unknown Packet";
            envelope.PayloadPreview = $"Non-decoded Meshtastic payload ({payloadLength} bytes)";
            return envelope;
        }

        var decoded = packet.Decoded;

        if (string.IsNullOrWhiteSpace(envelope.FromNodeId) && decoded.Source != 0)
        {
            envelope.FromNodeId = FormatNodeId(decoded.Source);
        }

        if (string.IsNullOrWhiteSpace(envelope.ToNodeId) && decoded.Dest != 0 && decoded.Dest != BroadcastNodeNumber)
        {
            envelope.ToNodeId = FormatNodeId(decoded.Dest);
            envelope.IsPrivate = true;
        }

        envelope.PacketType = GetPacketType(decoded.Portnum);
        envelope.PayloadPreview = BuildPayloadPreview(envelope, decoded);

        return envelope;
    }

    private string BuildPayloadPreview(MeshtasticEnvelope envelope, Data decoded)
    {
        return decoded.Portnum switch
        {
            PortNum.TextMessageApp => DecodeTextPayload(decoded.Payload),
            PortNum.TextMessageCompressedApp => $"Compressed text payload ({decoded.Payload.Length} bytes)",
            PortNum.PositionApp => DecodePositionPayload(envelope, decoded.Payload),
            PortNum.NodeinfoApp => DecodeNodeInfoPayload(envelope, decoded.Payload),
            PortNum.TelemetryApp => DecodeTelemetryPayload(envelope, decoded.Payload),
            _ => $"{GetPacketType(decoded.Portnum)} payload ({decoded.Payload.Length} bytes)"
        };
    }

    private string DecodeNodeInfoPayload(MeshtasticEnvelope envelope, ByteString payload)
    {
        try
        {
            var user = User.Parser.ParseFrom(payload);

            if (string.IsNullOrWhiteSpace(envelope.FromNodeId) && !string.IsNullOrWhiteSpace(user.Id))
            {
                envelope.FromNodeId = user.Id;
            }

            envelope.ShortName = NullIfWhiteSpace(user.ShortName);
            envelope.LongName = NullIfWhiteSpace(user.LongName);

            if (!string.IsNullOrWhiteSpace(envelope.LongName) && !string.IsNullOrWhiteSpace(envelope.ShortName))
            {
                return $"Node info: {envelope.LongName} ({envelope.ShortName})";
            }

            if (!string.IsNullOrWhiteSpace(envelope.LongName))
            {
                return $"Node info: {envelope.LongName}";
            }

            if (!string.IsNullOrWhiteSpace(envelope.ShortName))
            {
                return $"Node info: {envelope.ShortName}";
            }

            return "Node info update";
        }
        catch (InvalidProtocolBufferException exception)
        {
            _logger.LogDebug(exception, "Unable to decode Meshtastic node info payload");
            return $"Node info payload ({payload.Length} bytes)";
        }
    }

    private string DecodePositionPayload(MeshtasticEnvelope envelope, ByteString payload)
    {
        try
        {
            var position = Position.Parser.ParseFrom(payload);

            if (position.LatitudeI == 0 && position.LongitudeI == 0)
            {
                return "Position update";
            }

            envelope.Latitude = position.LatitudeI / 10_000_000d;
            envelope.Longitude = position.LongitudeI / 10_000_000d;

            return string.Create(
                CultureInfo.InvariantCulture,
                $"Position: {envelope.Latitude:F5}, {envelope.Longitude:F5}");
        }
        catch (InvalidProtocolBufferException exception)
        {
            _logger.LogDebug(exception, "Unable to decode Meshtastic position payload");
            return $"Position payload ({payload.Length} bytes)";
        }
    }

    private string DecodeTelemetryPayload(MeshtasticEnvelope envelope, ByteString payload)
    {
        try
        {
            var telemetry = Telemetry.Parser.ParseFrom(payload);

            return telemetry.VariantCase switch
            {
                Telemetry.VariantOneofCase.DeviceMetrics => DecodeDeviceMetrics(envelope, telemetry.DeviceMetrics),
                Telemetry.VariantOneofCase.EnvironmentMetrics => DecodeEnvironmentMetrics(envelope, telemetry.EnvironmentMetrics),
                _ => "Telemetry update"
            };
        }
        catch (InvalidProtocolBufferException exception)
        {
            _logger.LogDebug(exception, "Unable to decode Meshtastic telemetry payload");
            return $"Telemetry payload ({payload.Length} bytes)";
        }
    }

    private static string DecodeDeviceMetrics(MeshtasticEnvelope envelope, DeviceMetrics metrics)
    {
        envelope.BatteryLevelPercent = metrics.BatteryLevel == 0 && metrics.Voltage == 0
            ? null
            : (int?)metrics.BatteryLevel;
        envelope.Voltage = metrics.Voltage == 0 ? null : metrics.Voltage;
        envelope.ChannelUtilization = metrics.ChannelUtilization == 0 ? null : metrics.ChannelUtilization;
        envelope.AirUtilTx = metrics.AirUtilTx == 0 ? null : metrics.AirUtilTx;
        envelope.UptimeSeconds = metrics.UptimeSeconds == 0 ? null : metrics.UptimeSeconds;

        var parts = new List<string>();

        if (envelope.BatteryLevelPercent.HasValue)
        {
            parts.Add($"{envelope.BatteryLevelPercent.Value}% battery");
        }

        if (envelope.Voltage.HasValue)
        {
            parts.Add(string.Create(CultureInfo.InvariantCulture, $"{envelope.Voltage.Value:F2}V"));
        }

        if (envelope.ChannelUtilization.HasValue)
        {
            parts.Add(string.Create(CultureInfo.InvariantCulture, $"{envelope.ChannelUtilization.Value:F1}% channel"));
        }

        if (envelope.AirUtilTx.HasValue)
        {
            parts.Add(string.Create(CultureInfo.InvariantCulture, $"{envelope.AirUtilTx.Value:F1}% TX"));
        }

        if (envelope.UptimeSeconds.HasValue)
        {
            parts.Add($"uptime {FormatDuration(envelope.UptimeSeconds.Value)}");
        }

        return parts.Count == 0 ? "Telemetry update" : $"Device metrics: {string.Join(", ", parts)}";
    }

    private static string DecodeEnvironmentMetrics(MeshtasticEnvelope envelope, EnvironmentMetrics metrics)
    {
        envelope.TemperatureCelsius = metrics.Temperature == 0 ? null : metrics.Temperature;
        envelope.RelativeHumidity = metrics.RelativeHumidity == 0 ? null : metrics.RelativeHumidity;
        envelope.BarometricPressure = metrics.BarometricPressure == 0 ? null : metrics.BarometricPressure;

        var parts = new List<string>();

        if (envelope.TemperatureCelsius.HasValue)
        {
            parts.Add(string.Create(CultureInfo.InvariantCulture, $"{envelope.TemperatureCelsius.Value:F1}C"));
        }

        if (envelope.RelativeHumidity.HasValue)
        {
            parts.Add(string.Create(CultureInfo.InvariantCulture, $"{envelope.RelativeHumidity.Value:F1}% RH"));
        }

        if (envelope.BarometricPressure.HasValue)
        {
            parts.Add(string.Create(CultureInfo.InvariantCulture, $"{envelope.BarometricPressure.Value:F1} hPa"));
        }

        return parts.Count == 0 ? "Telemetry update" : $"Environment metrics: {string.Join(", ", parts)}";
    }

    private static string DecodeTextPayload(ByteString payload)
    {
        if (payload.IsEmpty)
        {
            return "(empty text message)";
        }

        var text = Encoding.UTF8.GetString(payload.Span);
        text = text.Replace("\r\n", " ", StringComparison.Ordinal)
            .Replace('\n', ' ')
            .Trim();

        return string.IsNullOrWhiteSpace(text) ? "(empty text message)" : text;
    }

    private static string GetPacketType(PortNum portNum)
    {
        return portNum switch
        {
            PortNum.TextMessageApp => "Text Message",
            PortNum.TextMessageCompressedApp => "Compressed Text Message",
            PortNum.PositionApp => "Position Update",
            PortNum.NodeinfoApp => "Node Info",
            PortNum.TelemetryApp => "Telemetry",
            _ => portNum.ToString()
        };
    }

    private static bool IsPrivate(uint targetNodeNumber)
    {
        return targetNodeNumber != 0 && targetNodeNumber != BroadcastNodeNumber;
    }

    private static string? ResolveTargetNodeId(uint targetNodeNumber)
    {
        return IsPrivate(targetNodeNumber) ? FormatNodeId(targetNodeNumber) : null;
    }

    private static DateTimeOffset ResolveReceivedAtUtc(uint rxTime)
    {
        return rxTime > 0
            ? DateTimeOffset.FromUnixTimeSeconds(rxTime)
            : DateTimeOffset.UtcNow;
    }

    private static string? FormatNodeId(uint nodeNumber)
    {
        return nodeNumber == 0 ? null : $"!{nodeNumber:x8}";
    }

    private static string? NullIfWhiteSpace(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static string FormatDuration(long uptimeSeconds)
    {
        var uptime = TimeSpan.FromSeconds(uptimeSeconds);

        if (uptime.TotalDays >= 1)
        {
            return $"{(int)uptime.TotalDays}d {uptime.Hours}h";
        }

        if (uptime.TotalHours >= 1)
        {
            return $"{(int)uptime.TotalHours}h {uptime.Minutes}m";
        }

        return $"{uptime.Minutes}m";
    }

    private static string? TryExtractNodeIdFromTopic(string topic)
    {
        if (string.IsNullOrWhiteSpace(topic))
        {
            return null;
        }

        var segments = topic.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (segments.Length == 0)
        {
            return null;
        }

        var lastSegment = segments[^1];
        return lastSegment.StartsWith('!') ? lastSegment : null;
    }

    private static bool TryParseMeshPacket(byte[] payload, out MeshPacket? meshPacket)
    {
        try
        {
            meshPacket = MeshPacket.Parser.ParseFrom(payload);
            return true;
        }
        catch (InvalidProtocolBufferException)
        {
            meshPacket = null;
            return false;
        }
    }

    private static bool TryParseServiceEnvelope(byte[] payload, out ServiceEnvelope? serviceEnvelope)
    {
        try
        {
            serviceEnvelope = ServiceEnvelope.Parser.ParseFrom(payload);
            return true;
        }
        catch (InvalidProtocolBufferException)
        {
            serviceEnvelope = null;
            return false;
        }
    }
}
