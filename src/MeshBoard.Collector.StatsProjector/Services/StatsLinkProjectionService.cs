using MeshBoard.Application.Abstractions.Persistence;
using MeshBoard.Contracts.CollectorEvents.Normalized;
using MeshBoard.Contracts.Meshtastic;

namespace MeshBoard.Collector.StatsProjector.Services;

public sealed class StatsLinkProjectionService : IStatsLinkProjectionService
{
    private readonly INeighborLinkRepository _neighborLinkRepository;
    private readonly ILogger<StatsLinkProjectionService> _logger;

    public StatsLinkProjectionService(
        INeighborLinkRepository neighborLinkRepository,
        ILogger<StatsLinkProjectionService> logger)
    {
        _neighborLinkRepository = neighborLinkRepository;
        _logger = logger;
    }

    public async Task ProjectAsync(LinkObserved link, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(link);

        _logger.LogDebug(
            "Projecting link {SourceNodeId} -> {TargetNodeId} on {ChannelKey}",
            link.SourceNodeId,
            link.TargetNodeId,
            link.ChannelKey);

        await _neighborLinkRepository.UpsertAsync(
            link.BrokerServer,
            link.ChannelKey,
            [
                new NeighborLinkRecord
                {
                    SourceNodeId = link.SourceNodeId,
                    TargetNodeId = link.TargetNodeId,
                    SnrDb = link.SnrDb,
                    LastSeenAtUtc = link.ObservedAtUtc
                }
            ],
            ct);
    }
}
