using MassTransit;
using MeshBoard.Application.Abstractions.Meshtastic;
using MeshBoard.Application.Collector;
using MeshBoard.Collector.Normalizer.Observability;
using MeshBoard.Contracts.CollectorEvents;
using MeshBoard.Contracts.CollectorEvents.DeadLetter;
using MeshBoard.Contracts.CollectorEvents.Normalized;
using MeshBoard.Contracts.CollectorEvents.RawPackets;
using MeshBoard.Contracts.Meshtastic;
using Microsoft.Extensions.Logging;

namespace MeshBoard.Collector.Normalizer.Services;

public sealed class PacketNormalizationService : IPacketNormalizationService
{
    private readonly IMeshtasticEnvelopeReader _envelopeReader;
    private readonly ICollectorChannelResolver _channelResolver;
    private readonly ILinkDerivationService _linkDerivationService;
    private readonly ITopicProducer<string, PacketNormalized> _packetNormalizedProducer;
    private readonly ITopicProducer<string, NodeObserved> _nodeObservedProducer;
    private readonly ITopicProducer<string, LinkObserved> _linkObservedProducer;
    private readonly ITopicProducer<string, TelemetryObserved> _telemetryObservedProducer;
    private readonly ITopicProducer<string, DeadLetterEvent> _deadLetterProducer;
    private readonly ILogger<PacketNormalizationService> _logger;

    public PacketNormalizationService(
        IMeshtasticEnvelopeReader envelopeReader,
        ICollectorChannelResolver channelResolver,
        ILinkDerivationService linkDerivationService,
        ITopicProducer<string, PacketNormalized> packetNormalizedProducer,
        ITopicProducer<string, NodeObserved> nodeObservedProducer,
        ITopicProducer<string, LinkObserved> linkObservedProducer,
        ITopicProducer<string, TelemetryObserved> telemetryObservedProducer,
        ITopicProducer<string, DeadLetterEvent> deadLetterProducer,
        ILogger<PacketNormalizationService> logger)
    {
        _envelopeReader = envelopeReader;
        _channelResolver = channelResolver;
        _linkDerivationService = linkDerivationService;
        _packetNormalizedProducer = packetNormalizedProducer;
        _nodeObservedProducer = nodeObservedProducer;
        _linkObservedProducer = linkObservedProducer;
        _telemetryObservedProducer = telemetryObservedProducer;
        _deadLetterProducer = deadLetterProducer;
        _logger = logger;
    }

    public async Task NormalizeAsync(RawPacketReceived rawPacket, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(rawPacket);

        var topicPattern = _channelResolver.ResolveTopicPattern(rawPacket.Topic) ?? rawPacket.Topic;
        var channelKey = _channelResolver.ResolveChannelKey(rawPacket.Topic) ?? string.Empty;
        var partitionKey = CollectorEventPartitionKeys.BuildChannelScope(rawPacket.BrokerServer, topicPattern);
        var envelope = await _envelopeReader.Read(
            string.Empty,
            rawPacket.Topic,
            rawPacket.Payload,
            cancellationToken);

        if (envelope is null)
        {
            _logger.LogWarning(
                "Failed to decode raw packet event {EventId} from broker {BrokerServer} topic {Topic}; routing to dead letter",
                rawPacket.EventId,
                rawPacket.BrokerServer,
                rawPacket.Topic);

            var deadLetterEvent = new DeadLetterEvent
            {
                EventId = Guid.NewGuid(),
                SchemaVersion = CollectorEventSchemaVersions.V1,
                BrokerServer = rawPacket.BrokerServer,
                CorrelationId = rawPacket.CorrelationId,
                TraceParent = rawPacket.TraceParent,
                OriginalTopic = rawPacket.Topic,
                Reason = "decode_failed",
                Detail = "Meshtastic envelope reader returned null.",
                OriginalReceivedAtUtc = rawPacket.ReceivedAtUtc
            };

            await ProduceAsync(_deadLetterProducer, deadLetterEvent, partitionKey, cancellationToken);
            NormalizerObservability.RecordDeadLettered();
            return;
        }

        envelope.BrokerServer = rawPacket.BrokerServer;
        envelope.ReceivedAtUtc = rawPacket.ReceivedAtUtc;

        var observedAtUtc = rawPacket.ReceivedAtUtc;
        var decodeStatus = PacketClassifier.ResolveDecodeStatus(envelope.PacketType);
        var decryptStatus = PacketClassifier.ResolveDecryptStatus(envelope.PacketType);
        var packetKey = BuildPacketKey(rawPacket, envelope);
        var topicIdentity = TryParseTopicIdentity(rawPacket.Topic);

        _logger.LogDebug(
            "Publishing normalized collector events for raw packet {EventId} from broker {BrokerServer} topic {Topic}",
            rawPacket.EventId,
            rawPacket.BrokerServer,
            rawPacket.Topic);

        try
        {
            var packetNormalized = new PacketNormalized
            {
                EventId = Guid.NewGuid(),
                SchemaVersion = CollectorEventSchemaVersions.V1,
                BrokerServer = rawPacket.BrokerServer,
                CorrelationId = rawPacket.CorrelationId,
                TraceParent = rawPacket.TraceParent,
                Topic = rawPacket.Topic,
                TopicPattern = topicPattern,
                PacketKey = packetKey,
                Region = topicIdentity?.Region,
                ChannelName = topicIdentity?.ChannelName,
                MeshVersion = topicIdentity?.MeshVersion,
                ReceivedAtUtc = rawPacket.ReceivedAtUtc,
                ObservedAtUtc = observedAtUtc,
                PacketId = envelope.PacketId,
                PacketType = envelope.PacketType,
                PayloadPreview = envelope.PayloadPreview,
                FromNodeId = envelope.FromNodeId,
                ToNodeId = envelope.ToNodeId,
                GatewayNodeId = envelope.GatewayNodeId,
                IsPrivate = envelope.IsPrivate,
                DecodeStatus = decodeStatus,
                DecryptStatus = decryptStatus,
                ShortName = envelope.ShortName,
                LongName = envelope.LongName,
                LastHeardChannel = envelope.LastHeardChannel,
                RxSnr = envelope.RxSnr,
                RxRssi = envelope.RxRssi,
                HopLimit = envelope.HopLimit,
                HopStart = envelope.HopStart,
                Neighbors = envelope.Neighbors ?? [],
                TracerouteHops = envelope.TracerouteHops ?? []
            };

            await ProduceAsync(_packetNormalizedProducer, packetNormalized, partitionKey, cancellationToken);
            NormalizerObservability.RecordDecodeSucceeded();

            if (decryptStatus == CollectorDecryptStatus.Failed)
            {
                NormalizerObservability.RecordDecryptFailed();
            }

            if (!string.IsNullOrWhiteSpace(envelope.FromNodeId))
            {
                var nodeObserved = new NodeObserved
                {
                    EventId = Guid.NewGuid(),
                    SchemaVersion = CollectorEventSchemaVersions.V1,
                    BrokerServer = rawPacket.BrokerServer,
                    CorrelationId = rawPacket.CorrelationId,
                    TraceParent = rawPacket.TraceParent,
                    TopicPattern = topicPattern,
                    NodeId = envelope.FromNodeId,
                    ObservedAtUtc = observedAtUtc,
                    ShortName = envelope.ShortName,
                    LongName = envelope.LongName,
                    Latitude = envelope.Latitude,
                    Longitude = envelope.Longitude,
                    LastHeardChannel = envelope.LastHeardChannel,
                    IsTextMessage = IsTextPacketType(envelope.PacketType)
                };

                await ProduceAsync(_nodeObservedProducer, nodeObserved, partitionKey, cancellationToken);
            }

            var linkOrigin = PacketClassifier.ResolveLinkOrigin(
                envelope.Neighbors?.Count > 0,
                envelope.TracerouteHops?.Count >= 2);
            var links = _linkDerivationService.DeriveLinks(envelope);

            foreach (var link in links)
            {
                var linkObserved = new LinkObserved
                {
                    EventId = Guid.NewGuid(),
                    SchemaVersion = CollectorEventSchemaVersions.V1,
                    BrokerServer = rawPacket.BrokerServer,
                    CorrelationId = rawPacket.CorrelationId,
                    TraceParent = rawPacket.TraceParent,
                    TopicPattern = topicPattern,
                    ChannelKey = channelKey,
                    SourceNodeId = link.SourceNodeId,
                    TargetNodeId = link.TargetNodeId,
                    ObservedAtUtc = observedAtUtc,
                    SnrDb = link.SnrDb,
                    LinkOrigin = linkOrigin
                };

                await ProduceAsync(_linkObservedProducer, linkObserved, partitionKey, cancellationToken);
            }

            foreach (var telemetry in BuildTelemetryEvents(rawPacket, topicPattern, envelope, observedAtUtc))
            {
                await ProduceAsync(_telemetryObservedProducer, telemetry, partitionKey, cancellationToken);
            }

            _logger.LogDebug(
                "Published normalized collector events for raw packet {EventId} with packet key {PacketKey}",
                rawPacket.EventId,
                packetKey);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogError(
                exception,
                "Failed to publish normalized collector events for raw packet {EventId} from broker {BrokerServer} topic {Topic}",
                rawPacket.EventId,
                rawPacket.BrokerServer,
                rawPacket.Topic);

            throw;
        }
    }

    private static IReadOnlyList<TelemetryObserved> BuildTelemetryEvents(
        RawPacketReceived rawPacket,
        string topicPattern,
        MeshtasticEnvelope envelope,
        DateTimeOffset observedAtUtc)
    {
        if (string.IsNullOrWhiteSpace(envelope.FromNodeId))
        {
            return [];
        }

        var telemetryEvents = new List<TelemetryObserved>();

        AddTelemetry(telemetryEvents, rawPacket, topicPattern, envelope.FromNodeId, observedAtUtc, "battery_level_percent", envelope.BatteryLevelPercent);
        AddTelemetry(telemetryEvents, rawPacket, topicPattern, envelope.FromNodeId, observedAtUtc, "voltage", envelope.Voltage);
        AddTelemetry(telemetryEvents, rawPacket, topicPattern, envelope.FromNodeId, observedAtUtc, "channel_utilization", envelope.ChannelUtilization);
        AddTelemetry(telemetryEvents, rawPacket, topicPattern, envelope.FromNodeId, observedAtUtc, "air_util_tx", envelope.AirUtilTx);
        AddTelemetry(telemetryEvents, rawPacket, topicPattern, envelope.FromNodeId, observedAtUtc, "uptime_seconds", envelope.UptimeSeconds);
        AddTelemetry(telemetryEvents, rawPacket, topicPattern, envelope.FromNodeId, observedAtUtc, "temperature_celsius", envelope.TemperatureCelsius);
        AddTelemetry(telemetryEvents, rawPacket, topicPattern, envelope.FromNodeId, observedAtUtc, "relative_humidity", envelope.RelativeHumidity);
        AddTelemetry(telemetryEvents, rawPacket, topicPattern, envelope.FromNodeId, observedAtUtc, "barometric_pressure", envelope.BarometricPressure);

        return telemetryEvents;
    }

    private static void AddTelemetry(
        ICollection<TelemetryObserved> telemetryEvents,
        RawPacketReceived rawPacket,
        string topicPattern,
        string nodeId,
        DateTimeOffset observedAtUtc,
        string metricType,
        int? metricValue)
    {
        if (!metricValue.HasValue)
        {
            return;
        }

        telemetryEvents.Add(CreateTelemetryObserved(
            rawPacket,
            topicPattern,
            nodeId,
            observedAtUtc,
            metricType,
            metricValue.Value));
    }

    private static void AddTelemetry(
        ICollection<TelemetryObserved> telemetryEvents,
        RawPacketReceived rawPacket,
        string topicPattern,
        string nodeId,
        DateTimeOffset observedAtUtc,
        string metricType,
        long? metricValue)
    {
        if (!metricValue.HasValue)
        {
            return;
        }

        telemetryEvents.Add(CreateTelemetryObserved(
            rawPacket,
            topicPattern,
            nodeId,
            observedAtUtc,
            metricType,
            metricValue.Value));
    }

    private static void AddTelemetry(
        ICollection<TelemetryObserved> telemetryEvents,
        RawPacketReceived rawPacket,
        string topicPattern,
        string nodeId,
        DateTimeOffset observedAtUtc,
        string metricType,
        double? metricValue)
    {
        if (!metricValue.HasValue || !double.IsFinite(metricValue.Value))
        {
            return;
        }

        telemetryEvents.Add(CreateTelemetryObserved(
            rawPacket,
            topicPattern,
            nodeId,
            observedAtUtc,
            metricType,
            metricValue.Value));
    }

    private static TelemetryObserved CreateTelemetryObserved(
        RawPacketReceived rawPacket,
        string topicPattern,
        string nodeId,
        DateTimeOffset observedAtUtc,
        string metricType,
        double metricValue)
    {
        return new TelemetryObserved
        {
            EventId = Guid.NewGuid(),
            SchemaVersion = CollectorEventSchemaVersions.V1,
            BrokerServer = rawPacket.BrokerServer,
            CorrelationId = rawPacket.CorrelationId,
            TraceParent = rawPacket.TraceParent,
            TopicPattern = topicPattern,
            NodeId = nodeId,
            ObservedAtUtc = observedAtUtc,
            MetricType = metricType,
            MetricValue = metricValue
        };
    }

    private static bool IsTextPacketType(string? packetType)
    {
        return string.Equals(packetType, "Text Message", StringComparison.Ordinal) ||
            string.Equals(packetType, "Compressed Text Message", StringComparison.Ordinal);
    }

    private static CollectorTopicIdentity? TryParseTopicIdentity(string topic)
    {
        if (string.IsNullOrWhiteSpace(topic))
        {
            return null;
        }

        var segments = topic.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (segments.Length < 5 || !string.Equals(segments[0], "msh", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return new CollectorTopicIdentity(
            string.IsNullOrWhiteSpace(segments[1]) ? null : segments[1],
            string.IsNullOrWhiteSpace(segments[2]) ? null : segments[2],
            string.IsNullOrWhiteSpace(segments[4]) ? null : segments[4]);
    }

    private static async Task ProduceAsync<TMessage>(
        ITopicProducer<string, TMessage> producer,
        TMessage message,
        string partitionKey,
        CancellationToken cancellationToken)
        where TMessage : class
    {
        await producer.Produce(partitionKey, message, cancellationToken);
    }

    private static string BuildPacketKey(RawPacketReceived rawPacket, MeshtasticEnvelope envelope)
    {
        return CollectorEventPacketKey.Build(
            rawPacket.BrokerServer,
            envelope.FromNodeId,
            envelope.PacketId,
            envelope.PacketType,
            envelope.ToNodeId,
            envelope.PayloadPreview,
            rawPacket.ReceivedAtUtc);
    }

    private sealed record CollectorTopicIdentity(
        string? Region,
        string? MeshVersion,
        string? ChannelName);
}
