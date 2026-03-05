using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Google.Protobuf;
using MeshBoard.Application.Abstractions.Meshtastic;
using MeshBoard.Contracts.Meshtastic;
using MeshBoard.Infrastructure.Meshtastic.Protobuf;
using Microsoft.Extensions.Logging;

namespace MeshBoard.Infrastructure.Meshtastic.Decoding;

internal sealed class MeshtasticEnvelopeReader : IMeshtasticEnvelopeReader
{
    private const uint BroadcastNodeNumber = uint.MaxValue;
    private readonly ITopicEncryptionKeyResolver _topicEncryptionKeyResolver;
    private readonly ILogger<MeshtasticEnvelopeReader> _logger;

    public MeshtasticEnvelopeReader(
        ITopicEncryptionKeyResolver topicEncryptionKeyResolver,
        ILogger<MeshtasticEnvelopeReader> logger)
    {
        _topicEncryptionKeyResolver = topicEncryptionKeyResolver;
        _logger = logger;
    }

    public async Task<MeshtasticEnvelope?> Read(
        string topic,
        byte[] payload,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await ReadCore(topic, payload, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogDebug(
                exception,
                "Skipping Meshtastic payload that could not be decoded for topic: {Topic}",
                topic);

            return null;
        }
    }

    private async Task<MeshtasticEnvelope?> ReadCore(
        string topic,
        byte[] payload,
        CancellationToken cancellationToken)
    {
        if (TryParseServiceEnvelope(payload, out var serviceEnvelope) &&
            serviceEnvelope is not null &&
            serviceEnvelope.Packet is not null &&
            HasPacketContent(serviceEnvelope.Packet))
        {
            return await MapEnvelope(topic, serviceEnvelope.Packet, payload.Length, cancellationToken);
        }

        if (TryParseMeshPacket(payload, out var directPacket) && directPacket is not null)
        {
            return await MapEnvelope(topic, directPacket, payload.Length, cancellationToken);
        }

        if (TryParseJsonEnvelope(topic, payload, out var jsonEnvelope) && jsonEnvelope is not null)
        {
            return jsonEnvelope;
        }

        return null;
    }

    private async Task<MeshtasticEnvelope> MapEnvelope(
        string topic,
        MeshPacket packet,
        int payloadLength,
        CancellationToken cancellationToken)
    {
        var envelope = new MeshtasticEnvelope
        {
            Topic = topic,
            PacketId = packet.Id == 0 ? null : packet.Id,
            FromNodeId = FormatNodeId(packet.From) ?? TryExtractNodeIdFromTopic(topic),
            LastHeardChannel = TryExtractChannelFromTopic(topic),
            ToNodeId = ResolveTargetNodeId(packet.To),
            IsPrivate = IsPrivate(packet.To),
            ReceivedAtUtc = ResolveReceivedAtUtc(packet.RxTime)
        };

        var decodedPayload = packet.PayloadVariantCase switch
        {
            MeshPacket.PayloadVariantOneofCase.Decoded => packet.Decoded,
            MeshPacket.PayloadVariantOneofCase.Encrypted => await TryDecryptPayload(
                topic,
                packet,
                cancellationToken),
            _ => null
        };

        if (decodedPayload is null)
        {
            envelope.PacketType = packet.PayloadVariantCase == MeshPacket.PayloadVariantOneofCase.Encrypted
                ? "Encrypted Packet"
                : "Unknown Packet";
            envelope.PayloadPreview = $"Non-decoded Meshtastic payload ({payloadLength} bytes)";
            return envelope;
        }

        var decoded = decodedPayload;

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

    private async Task<Data?> TryDecryptPayload(
        string topic,
        MeshPacket packet,
        CancellationToken cancellationToken)
    {
        if (packet.Encrypted.IsEmpty || packet.From == 0 || packet.Id == 0)
        {
            return null;
        }

        var nonce = BuildNonce(packet.From, packet.Id);
        var candidateKeys = await _topicEncryptionKeyResolver.ResolveCandidateKeysAsync(topic, cancellationToken);

        foreach (var key in candidateKeys)
        {
            try
            {
                var decryptedBytes = AesCtrCipher.Transform(packet.Encrypted.Span, nonce, key);
                return Data.Parser.ParseFrom(decryptedBytes);
            }
            catch (Exception exception) when (exception is CryptographicException or InvalidProtocolBufferException)
            {
                _logger.LogDebug(
                    exception,
                    "Failed to decrypt Meshtastic packet for topic {Topic} with a candidate key",
                    topic);
            }
        }

        return null;
    }

    private static byte[] BuildNonce(uint fromNodeNumber, uint packetId)
    {
        var nonce = new byte[16];
        Buffer.BlockCopy(BitConverter.GetBytes((ulong)packetId), 0, nonce, 0, sizeof(ulong));
        Buffer.BlockCopy(BitConverter.GetBytes(fromNodeNumber), 0, nonce, sizeof(ulong), sizeof(uint));
        return nonce;
    }

    private bool TryParseJsonEnvelope(string topic, byte[] payload, out MeshtasticEnvelope? envelope)
    {
        var trimmedPayload = TrimLeadingWhitespace(payload);

        if (trimmedPayload.Length == 0 || trimmedPayload[0] != '{')
        {
            envelope = null;
            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(payload);

            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                envelope = null;
                return false;
            }

            envelope = MapJsonEnvelope(topic, document.RootElement, payload.Length);
            return true;
        }
        catch (JsonException)
        {
            envelope = null;
            return false;
        }
    }

    private MeshtasticEnvelope MapJsonEnvelope(string topic, JsonElement root, int payloadLength)
    {
        var envelope = new MeshtasticEnvelope
        {
            Topic = topic,
            LastHeardChannel = TryExtractChannelFromTopic(topic),
            FromNodeId = TryGetNodeId(root, "fromId", "from_id", "from", "sender"),
            ToNodeId = TryGetNodeId(root, "toId", "to_id", "to"),
            PacketId = TryGetUInt(root, "id", "packetId", "packet_id"),
            ReceivedAtUtc = ResolveJsonReceivedAtUtc(root)
        };

        if (TryGetProperty(root, out var decodedElement, "decoded") && decodedElement.ValueKind == JsonValueKind.Object)
        {
            envelope.FromNodeId ??= TryGetNodeId(decodedElement, "source", "from", "fromId");
            envelope.ToNodeId ??= TryGetNodeId(decodedElement, "dest", "to", "toId");
            envelope.PacketId ??= TryGetUInt(decodedElement, "id", "packetId", "packet_id");
        }

        envelope.FromNodeId ??= TryExtractNodeIdFromTopic(topic);

        if (IsBroadcastNodeId(envelope.ToNodeId))
        {
            envelope.ToNodeId = null;
        }

        envelope.IsPrivate = !string.IsNullOrWhiteSpace(envelope.ToNodeId);

        if (TryMapJsonDecodedPayload(root, envelope, out var decodedPacketType, out var decodedPayloadPreview))
        {
            envelope.PacketType = decodedPacketType;
            envelope.PayloadPreview = decodedPayloadPreview;
            return envelope;
        }

        var typeToken = TryGetString(root, "type", "packetType", "packet_type");
        envelope.PacketType = MapJsonTypeToPacketType(typeToken);
        envelope.PayloadPreview = BuildJsonPayloadPreview(envelope.PacketType, root, payloadLength);

        return envelope;
    }

    private bool TryMapJsonDecodedPayload(
        JsonElement root,
        MeshtasticEnvelope envelope,
        out string packetType,
        out string payloadPreview)
    {
        packetType = string.Empty;
        payloadPreview = string.Empty;

        if (!TryGetProperty(root, out var decodedElement, "decoded") ||
            decodedElement.ValueKind != JsonValueKind.Object ||
            !TryGetProperty(decodedElement, out var portElement, "portnum", "portNum") ||
            !TryParsePortNum(portElement, out var portNum))
        {
            return false;
        }

        if (TryGetProperty(decodedElement, out var payloadElement, "payload") &&
            payloadElement.ValueKind == JsonValueKind.String)
        {
            var payloadString = payloadElement.GetString() ?? string.Empty;

            if (TryDecodeBase64(payloadString, out var payloadBytes))
            {
                var decodedData = new Data
                {
                    Portnum = portNum,
                    Payload = ByteString.CopyFrom(payloadBytes)
                };

                packetType = GetPacketType(portNum);
                payloadPreview = BuildPayloadPreview(envelope, decodedData);
                return true;
            }

            if (portNum == PortNum.TextMessageApp)
            {
                packetType = "Text Message";
                payloadPreview = NormalizeTextPayload(payloadString);
                return true;
            }
        }

        if (portNum == PortNum.TextMessageApp)
        {
            var text = TryGetString(decodedElement, "text", "message") ??
                TryGetString(root, "payload", "text", "message");

            if (!string.IsNullOrWhiteSpace(text))
            {
                packetType = "Text Message";
                payloadPreview = NormalizeTextPayload(text);
                return true;
            }
        }

        packetType = GetPacketType(portNum);
        payloadPreview = $"{packetType} payload (json)";
        return true;
    }

    private static string BuildJsonPayloadPreview(string packetType, JsonElement root, int payloadLength)
    {
        return packetType switch
        {
            "Text Message" => BuildJsonTextPayloadPreview(root, payloadLength),
            _ => $"JSON {packetType.ToLowerInvariant()} payload ({payloadLength} bytes)"
        };
    }

    private static string BuildJsonTextPayloadPreview(JsonElement root, int payloadLength)
    {
        if (TryGetProperty(root, out var payloadElement, "payload"))
        {
            if (payloadElement.ValueKind == JsonValueKind.String)
            {
                var payloadText = payloadElement.GetString() ?? string.Empty;

                if (TryDecodeBase64(payloadText, out var decodedBytes))
                {
                    return NormalizeTextPayload(Encoding.UTF8.GetString(decodedBytes));
                }

                return NormalizeTextPayload(payloadText);
            }

            if (payloadElement.ValueKind == JsonValueKind.Object)
            {
                var text = TryGetString(payloadElement, "text", "message");

                if (!string.IsNullOrWhiteSpace(text))
                {
                    return NormalizeTextPayload(text);
                }
            }
        }

        var fallbackText = TryGetString(root, "text", "message");
        return !string.IsNullOrWhiteSpace(fallbackText)
            ? NormalizeTextPayload(fallbackText)
            : $"Text message payload ({payloadLength} bytes)";
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

        return NormalizeTextPayload(Encoding.UTF8.GetString(payload.Span));
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

    private static string? TryExtractChannelFromTopic(string topic)
    {
        if (string.IsNullOrWhiteSpace(topic))
        {
            return null;
        }

        var segments = topic.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (segments.Length < 5)
        {
            return null;
        }

        var topicType = segments[3];

        if (!string.Equals(segments[0], "msh", StringComparison.OrdinalIgnoreCase) ||
            (!string.Equals(topicType, "e", StringComparison.OrdinalIgnoreCase) &&
             !string.Equals(topicType, "json", StringComparison.OrdinalIgnoreCase)))
        {
            return null;
        }

        var region = segments[1];
        var channel = segments[4];

        if (string.IsNullOrWhiteSpace(region) || string.IsNullOrWhiteSpace(channel))
        {
            return null;
        }

        return $"{region}/{channel}";
    }

    private static string NormalizeTextPayload(string value)
    {
        var text = value.Replace("\r\n", " ", StringComparison.Ordinal)
            .Replace('\n', ' ')
            .Trim();

        return string.IsNullOrWhiteSpace(text) ? "(empty text message)" : text;
    }

    private static DateTimeOffset ResolveJsonReceivedAtUtc(JsonElement root)
    {
        if (TryGetProperty(root, out var timestampElement, "rxTime", "rx_time", "timestamp", "receivedAt", "received_at"))
        {
            if (timestampElement.ValueKind == JsonValueKind.Number && timestampElement.TryGetInt64(out var unixSeconds))
            {
                return unixSeconds > 0 ? DateTimeOffset.FromUnixTimeSeconds(unixSeconds) : DateTimeOffset.UtcNow;
            }

            if (timestampElement.ValueKind == JsonValueKind.String)
            {
                var timestampText = timestampElement.GetString();

                if (long.TryParse(timestampText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedUnixSeconds))
                {
                    return parsedUnixSeconds > 0 ? DateTimeOffset.FromUnixTimeSeconds(parsedUnixSeconds) : DateTimeOffset.UtcNow;
                }

                if (DateTimeOffset.TryParse(
                    timestampText,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.RoundtripKind,
                    out var parsedTimestamp))
                {
                    return parsedTimestamp;
                }
            }
        }

        return DateTimeOffset.UtcNow;
    }

    private static string MapJsonTypeToPacketType(string? jsonType)
    {
        if (string.IsNullOrWhiteSpace(jsonType))
        {
            return "Unknown Packet";
        }

        var normalized = NormalizeToken(jsonType);

        return normalized switch
        {
            "text" or "textmessage" or "textmessageapp" => "Text Message",
            "nodeinfo" or "nodeinformation" or "nodeinfoapp" => "Node Info",
            "position" or "positionupdate" or "positionapp" => "Position Update",
            "telemetry" or "telemetryapp" => "Telemetry",
            "encrypted" or "encryptedpacket" => "Encrypted Packet",
            _ => jsonType
        };
    }

    private static bool TryParsePortNum(JsonElement element, out PortNum portNum)
    {
        if (element.ValueKind == JsonValueKind.Number && element.TryGetInt32(out var numericValue))
        {
            portNum = Enum.IsDefined(typeof(PortNum), numericValue)
                ? (PortNum)numericValue
                : PortNum.UnknownApp;
            return true;
        }

        if (element.ValueKind == JsonValueKind.String)
        {
            var value = element.GetString();

            if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out numericValue))
            {
                portNum = Enum.IsDefined(typeof(PortNum), numericValue)
                    ? (PortNum)numericValue
                    : PortNum.UnknownApp;
                return true;
            }

            var normalized = NormalizeToken(value);
            portNum = normalized switch
            {
                "text" or "textmessageapp" => PortNum.TextMessageApp,
                "textmessagecompressedapp" => PortNum.TextMessageCompressedApp,
                "position" or "positionapp" => PortNum.PositionApp,
                "nodeinfo" or "nodeinfoapp" => PortNum.NodeinfoApp,
                "telemetry" or "telemetryapp" => PortNum.TelemetryApp,
                _ => PortNum.UnknownApp
            };

            return true;
        }

        portNum = PortNum.UnknownApp;
        return false;
    }

    private static string NormalizeToken(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        return value
            .Replace("_", string.Empty, StringComparison.Ordinal)
            .Replace("-", string.Empty, StringComparison.Ordinal)
            .Replace(" ", string.Empty, StringComparison.Ordinal)
            .ToLowerInvariant();
    }

    private static bool TryDecodeBase64(string value, out byte[] decodedBytes)
    {
        try
        {
            decodedBytes = Convert.FromBase64String(value);
            return true;
        }
        catch (FormatException)
        {
            decodedBytes = [];
            return false;
        }
    }

    private static string? TryGetNodeId(JsonElement element, params string[] propertyNames)
    {
        if (!TryGetProperty(element, out var valueElement, propertyNames))
        {
            return null;
        }

        return valueElement.ValueKind switch
        {
            JsonValueKind.String => NormalizeNodeId(valueElement.GetString()),
            JsonValueKind.Number => valueElement.TryGetUInt32(out var nodeNumber) ? FormatNodeId(nodeNumber) : null,
            _ => null
        };
    }

    private static uint? TryGetUInt(JsonElement element, params string[] propertyNames)
    {
        if (!TryGetProperty(element, out var valueElement, propertyNames))
        {
            return null;
        }

        if (valueElement.ValueKind == JsonValueKind.Number && valueElement.TryGetUInt32(out var numericValue))
        {
            return numericValue;
        }

        if (valueElement.ValueKind == JsonValueKind.String &&
            uint.TryParse(valueElement.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out numericValue))
        {
            return numericValue;
        }

        return null;
    }

    private static string? TryGetString(JsonElement element, params string[] propertyNames)
    {
        if (!TryGetProperty(element, out var valueElement, propertyNames))
        {
            return null;
        }

        return valueElement.ValueKind == JsonValueKind.String ? valueElement.GetString() : null;
    }

    private static bool TryGetProperty(JsonElement element, out JsonElement value, params string[] propertyNames)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            value = default;
            return false;
        }

        foreach (var property in element.EnumerateObject())
        {
            if (propertyNames.Any(
                    propertyName => string.Equals(property.Name, propertyName, StringComparison.OrdinalIgnoreCase)))
            {
                value = property.Value;
                return true;
            }
        }

        value = default;
        return false;
    }

    private static string? NormalizeNodeId(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();

        if (trimmed.StartsWith('!'))
        {
            return trimmed;
        }

        if (uint.TryParse(trimmed, NumberStyles.Integer, CultureInfo.InvariantCulture, out var decimalNode))
        {
            return FormatNodeId(decimalNode);
        }

        if (trimmed.StartsWith("0x", StringComparison.OrdinalIgnoreCase) &&
            uint.TryParse(trimmed[2..], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var hexNode))
        {
            return FormatNodeId(hexNode);
        }

        return trimmed;
    }

    private static bool IsBroadcastNodeId(string? nodeId)
    {
        return string.Equals(nodeId, "!ffffffff", StringComparison.OrdinalIgnoreCase);
    }

    private static ReadOnlySpan<byte> TrimLeadingWhitespace(ReadOnlySpan<byte> value)
    {
        var startIndex = 0;

        while (startIndex < value.Length && char.IsWhiteSpace((char)value[startIndex]))
        {
            startIndex++;
        }

        return value[startIndex..];
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

    private static bool HasPacketContent(MeshPacket packet)
    {
        return packet.From != 0 ||
            packet.To != 0 ||
            packet.Id != 0 ||
            packet.PayloadVariantCase != MeshPacket.PayloadVariantOneofCase.None;
    }
}
