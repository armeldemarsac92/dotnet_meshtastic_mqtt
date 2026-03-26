using System.Diagnostics;
using MeshBoard.Collector.GraphProjector.Observability;
using MeshBoard.Contracts.CollectorEvents.Normalized;
using MeshBoard.Infrastructure.Neo4j.Repositories;

namespace MeshBoard.Collector.GraphProjector.Services;

public sealed class GraphLinkProjectionService : IGraphLinkProjectionService
{
    private readonly IGraphTopologyRepository _graphTopologyRepository;
    private readonly ILogger<GraphLinkProjectionService> _logger;

    public GraphLinkProjectionService(
        IGraphTopologyRepository graphTopologyRepository,
        ILogger<GraphLinkProjectionService> logger)
    {
        _graphTopologyRepository = graphTopologyRepository;
        _logger = logger;
    }

    public async Task ProjectAsync(LinkObserved link, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(link);
        var startedAt = Stopwatch.GetTimestamp();

        _logger.LogDebug(
            "Projecting link {SourceNodeId} -> {TargetNodeId} on {ChannelKey}",
            link.SourceNodeId,
            link.TargetNodeId,
            link.ChannelKey);

        await _graphTopologyRepository.UpsertLinkAsync(
            new GraphLinkWriteRequest
            {
                BrokerServer = link.BrokerServer,
                ChannelKey = link.ChannelKey,
                TopicPattern = string.IsNullOrWhiteSpace(link.TopicPattern) ? link.ChannelKey : link.TopicPattern,
                SourceNodeId = link.SourceNodeId,
                TargetNodeId = link.TargetNodeId,
                ObservedAtUtc = link.ObservedAtUtc,
                SnrDb = link.SnrDb,
                LinkOrigin = link.LinkOrigin.ToString()
            },
            ct);

        GraphProjectorObservability.RecordLinkUpserted();
        GraphProjectorObservability.RecordWriteCompleted(
            Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds);
    }
}
