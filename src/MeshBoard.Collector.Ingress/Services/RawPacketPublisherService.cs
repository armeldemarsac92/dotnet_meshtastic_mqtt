using MassTransit;
using MeshBoard.Collector.Ingress.Observability;
using MeshBoard.Contracts.CollectorEvents;
using MeshBoard.Contracts.CollectorEvents.RawPackets;
using MeshBoard.Contracts.Meshtastic;
using Microsoft.Extensions.DependencyInjection;

namespace MeshBoard.Collector.Ingress.Services;

public sealed class RawPacketPublisherService : IRawPacketPublisherService
{
    private static readonly string CollectorInstanceId = Environment.MachineName;

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<RawPacketPublisherService> _logger;

    public RawPacketPublisherService(
        IServiceScopeFactory scopeFactory,
        ILogger<RawPacketPublisherService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task PublishAsync(
        MqttInboundMessage inboundMessage,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(inboundMessage);

        var eventId = Guid.NewGuid();
        var partitionKey = CollectorEventPartitionKeys.BuildChannelScope(
            inboundMessage.BrokerServer,
            inboundMessage.Topic);
        var message = new RawPacketReceived
        {
            EventId = eventId,
            BrokerServer = inboundMessage.BrokerServer,
            Topic = inboundMessage.Topic,
            Payload = inboundMessage.Payload,
            ReceivedAtUtc = inboundMessage.ReceivedAtUtc,
            CollectorInstanceId = CollectorInstanceId,
            CorrelationId = Guid.NewGuid().ToString("N")
        };

        _logger.LogDebug(
            "Publishing raw packet event {EventId} for broker {BrokerServer} topic {Topic}",
            eventId,
            inboundMessage.BrokerServer,
            inboundMessage.Topic);

        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var producer = scope.ServiceProvider.GetRequiredService<ITopicProducer<string, RawPacketReceived>>();

            await producer.Produce(partitionKey, message, cancellationToken);

            IngressObservability.RecordPublishSucceeded(inboundMessage);

            _logger.LogDebug(
                "Published raw packet event {EventId} for topic {Topic}",
                eventId,
                inboundMessage.Topic);
        }
        catch (Exception exception)
        {
            IngressObservability.RecordPublishFailure(inboundMessage);

            _logger.LogError(
                exception,
                "Failed to publish raw packet event for topic {Topic}",
                inboundMessage.Topic);

            throw;
        }
    }
}
