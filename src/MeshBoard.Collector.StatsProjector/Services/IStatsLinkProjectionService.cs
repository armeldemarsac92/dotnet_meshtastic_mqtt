using MeshBoard.Contracts.CollectorEvents.Normalized;

namespace MeshBoard.Collector.StatsProjector.Services;

public interface IStatsLinkProjectionService
{
    Task ProjectAsync(LinkObserved link, CancellationToken ct = default);
}
