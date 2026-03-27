using MassTransit;
using MeshBoard.Collector.Normalizer.Services;
using MeshBoard.Contracts.CollectorEvents.RawPackets;

namespace MeshBoard.Collector.Normalizer.Consumers;

public sealed class RawPacketReceivedConsumer : IConsumer<RawPacketReceived>
{
    private readonly IPacketNormalizationService _normalizationService;
    private readonly ILogger<RawPacketReceivedConsumer> _logger;

    public RawPacketReceivedConsumer(
        IPacketNormalizationService normalizationService,
        ILogger<RawPacketReceivedConsumer> logger)
    {
        _normalizationService = normalizationService;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<RawPacketReceived> context)
    {
        _logger.LogDebug(
            "Consuming raw packet event {EventId} from broker {BrokerServer} topic {Topic}",
            context.Message.EventId,
            context.Message.BrokerServer,
            context.Message.Topic);

        await _normalizationService.NormalizeAsync(context.Message, context.CancellationToken);

        _logger.LogDebug(
            "Finished raw packet event {EventId}",
            context.Message.EventId);
    }
}
