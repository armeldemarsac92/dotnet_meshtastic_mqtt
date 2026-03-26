using MeshBoard.Contracts.CollectorEvents.Normalized;
using MeshBoard.Collector.GraphProjector.Observability;
using MeshBoard.Infrastructure.Neo4j.Repositories;

namespace MeshBoard.Collector.GraphProjector.Services;

public sealed class GraphNodeProjectionService : IGraphNodeProjectionService
{
    private readonly IGraphTopologyRepository _graphTopologyRepository;
    private readonly ILogger<GraphNodeProjectionService> _logger;

    public GraphNodeProjectionService(
        IGraphTopologyRepository graphTopologyRepository,
        ILogger<GraphNodeProjectionService> logger)
    {
        _graphTopologyRepository = graphTopologyRepository;
        _logger = logger;
    }

    public async Task ProjectAsync(NodeObserved node, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(node);

        _logger.LogDebug(
            "Projecting node {NodeId} from broker {BrokerServer}",
            node.NodeId,
            node.BrokerServer);

        await _graphTopologyRepository.UpsertNodeAsync(
            new GraphNodeWriteRequest
            {
                BrokerServer = node.BrokerServer,
                NodeId = node.NodeId,
                ShortName = node.ShortName,
                LongName = node.LongName,
                LastHeardAtUtc = node.IsTextMessage ? null : node.ObservedAtUtc,
                LastTextMessageAtUtc = node.IsTextMessage ? node.ObservedAtUtc : null,
                Latitude = node.Latitude,
                Longitude = node.Longitude,
                ChannelTopicPattern = string.IsNullOrWhiteSpace(node.LastHeardChannel) ? null : node.LastHeardChannel,
                ObservedAtUtc = node.ObservedAtUtc
            },
            ct);

        GraphProjectorObservability.RecordNodeUpserted();
    }
}
