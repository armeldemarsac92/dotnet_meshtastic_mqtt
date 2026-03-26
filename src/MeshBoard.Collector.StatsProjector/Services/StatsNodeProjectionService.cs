using MeshBoard.Application.Abstractions.Persistence;
using MeshBoard.Collector.StatsProjector.Observability;
using MeshBoard.Contracts.CollectorEvents.Normalized;
using MeshBoard.Contracts.Nodes;

namespace MeshBoard.Collector.StatsProjector.Services;

public sealed class StatsNodeProjectionService : IStatsNodeProjectionService
{
    private readonly INodeRepository _nodeRepository;
    private readonly ILogger<StatsNodeProjectionService> _logger;

    public StatsNodeProjectionService(
        INodeRepository nodeRepository,
        ILogger<StatsNodeProjectionService> logger)
    {
        _nodeRepository = nodeRepository;
        _logger = logger;
    }

    public async Task ProjectAsync(NodeObserved node, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(node);

        _logger.LogDebug(
            "Projecting node {NodeId} from broker {BrokerServer}",
            node.NodeId,
            node.BrokerServer);

        await _nodeRepository.UpsertAsync(
            new UpsertObservedNodeRequest
            {
                NodeId = node.NodeId,
                BrokerServer = node.BrokerServer,
                ShortName = node.ShortName,
                LongName = node.LongName,
                LastHeardAtUtc = node.IsTextMessage ? null : node.ObservedAtUtc,
                LastTextMessageAtUtc = node.IsTextMessage ? node.ObservedAtUtc : null,
                LastHeardChannel = node.LastHeardChannel,
                LastKnownLatitude = node.Latitude,
                LastKnownLongitude = node.Longitude
            },
            ct);

        StatsProjectorObservability.RecordNodeUpserted();
    }
}
