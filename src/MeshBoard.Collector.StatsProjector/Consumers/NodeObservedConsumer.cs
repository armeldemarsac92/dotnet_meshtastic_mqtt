using MassTransit;
using MeshBoard.Collector.StatsProjector.Services;
using MeshBoard.Contracts.CollectorEvents.Normalized;

namespace MeshBoard.Collector.StatsProjector.Consumers;

public sealed class NodeObservedConsumer : IConsumer<NodeObserved>
{
    private readonly IStatsNodeProjectionService _projectionService;
    private readonly ILogger<NodeObservedConsumer> _logger;

    public NodeObservedConsumer(
        IStatsNodeProjectionService projectionService,
        ILogger<NodeObservedConsumer> logger)
    {
        _projectionService = projectionService;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<NodeObserved> context)
    {
        _logger.LogDebug(
            "Consuming node observed event {EventId} for node {NodeId}",
            context.Message.EventId,
            context.Message.NodeId);

        await _projectionService.ProjectAsync(context.Message, context.CancellationToken);

        _logger.LogDebug(
            "Finished node observed event {EventId} for node {NodeId}",
            context.Message.EventId,
            context.Message.NodeId);
    }
}
