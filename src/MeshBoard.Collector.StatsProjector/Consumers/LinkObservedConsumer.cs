using MassTransit;
using MeshBoard.Collector.StatsProjector.Services;
using MeshBoard.Contracts.CollectorEvents.Normalized;

namespace MeshBoard.Collector.StatsProjector.Consumers;

public sealed class LinkObservedConsumer : IConsumer<LinkObserved>
{
    private readonly IStatsLinkProjectionService _projectionService;
    private readonly ILogger<LinkObservedConsumer> _logger;

    public LinkObservedConsumer(
        IStatsLinkProjectionService projectionService,
        ILogger<LinkObservedConsumer> logger)
    {
        _projectionService = projectionService;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<LinkObserved> context)
    {
        _logger.LogDebug(
            "Consuming link observed event {EventId} for {SourceNodeId} -> {TargetNodeId}",
            context.Message.EventId,
            context.Message.SourceNodeId,
            context.Message.TargetNodeId);

        await _projectionService.ProjectAsync(context.Message, context.CancellationToken);

        _logger.LogDebug(
            "Finished link observed event {EventId} for {SourceNodeId} -> {TargetNodeId}",
            context.Message.EventId,
            context.Message.SourceNodeId,
            context.Message.TargetNodeId);
    }
}
