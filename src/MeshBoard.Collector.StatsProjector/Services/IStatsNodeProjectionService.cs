using MeshBoard.Contracts.CollectorEvents.Normalized;

namespace MeshBoard.Collector.StatsProjector.Services;

public interface IStatsNodeProjectionService
{
    Task ProjectAsync(NodeObserved node, CancellationToken ct = default);
}
