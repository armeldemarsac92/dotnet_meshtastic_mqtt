using MassTransit;
using MeshBoard.Collector.StatsProjector.Services;
using MeshBoard.Contracts.CollectorEvents.Normalized;

namespace MeshBoard.Collector.StatsProjector.Consumers;

public sealed class TelemetryObservedConsumer : IConsumer<TelemetryObserved>
{
    private readonly IStatsTelemetryProjectionService _projectionService;
    private readonly ILogger<TelemetryObservedConsumer> _logger;

    public TelemetryObservedConsumer(
        IStatsTelemetryProjectionService projectionService,
        ILogger<TelemetryObservedConsumer> logger)
    {
        _projectionService = projectionService;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<TelemetryObserved> context)
    {
        _logger.LogDebug(
            "Consuming telemetry observed event {EventId} for node {NodeId} metric {MetricType}",
            context.Message.EventId,
            context.Message.NodeId,
            context.Message.MetricType);

        await _projectionService.ProjectAsync(context.Message, context.CancellationToken);

        _logger.LogDebug(
            "Finished telemetry observed event {EventId} for node {NodeId} metric {MetricType}",
            context.Message.EventId,
            context.Message.NodeId,
            context.Message.MetricType);
    }
}
