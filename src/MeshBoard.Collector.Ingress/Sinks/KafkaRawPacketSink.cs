using MeshBoard.Application.Abstractions.Meshtastic;
using MeshBoard.Collector.Ingress.Services;
using MeshBoard.Contracts.Meshtastic;

namespace MeshBoard.Collector.Ingress.Sinks;

public sealed class KafkaRawPacketSink : IMqttInboundMessageSink
{
    private readonly IRawPacketPublisherService _publisherService;
    private readonly ILogger<KafkaRawPacketSink> _logger;

    public KafkaRawPacketSink(
        IRawPacketPublisherService publisherService,
        ILogger<KafkaRawPacketSink> logger)
    {
        _publisherService = publisherService;
        _logger = logger;
    }

    public async Task HandleAsync(MqttInboundMessage inboundMessage)
    {
        ArgumentNullException.ThrowIfNull(inboundMessage);

        try
        {
            await _publisherService.PublishAsync(inboundMessage, CancellationToken.None);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(
                exception,
                "Skipping raw packet publish failure for topic {Topic}",
                inboundMessage.Topic);
        }
    }
}
