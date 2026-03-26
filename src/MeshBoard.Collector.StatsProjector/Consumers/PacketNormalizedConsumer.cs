using MassTransit;
using MeshBoard.Collector.StatsProjector.Services;
using MeshBoard.Contracts.CollectorEvents.Normalized;

namespace MeshBoard.Collector.StatsProjector.Consumers;

public sealed class PacketNormalizedConsumer : IConsumer<PacketNormalized>
{
    private readonly IStatsPacketProjectionService _projectionService;
    private readonly ILogger<PacketNormalizedConsumer> _logger;

    public PacketNormalizedConsumer(
        IStatsPacketProjectionService projectionService,
        ILogger<PacketNormalizedConsumer> logger)
    {
        _projectionService = projectionService;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<PacketNormalized> context)
    {
        _logger.LogDebug(
            "Consuming packet normalized event {EventId} with packet key {PacketKey}",
            context.Message.EventId,
            context.Message.PacketKey);

        await _projectionService.ProjectAsync(context.Message, context.CancellationToken);

        _logger.LogDebug(
            "Finished packet normalized event {EventId} with packet key {PacketKey}",
            context.Message.EventId,
            context.Message.PacketKey);
    }
}
