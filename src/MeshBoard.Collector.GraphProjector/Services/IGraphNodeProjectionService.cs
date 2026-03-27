using MeshBoard.Contracts.CollectorEvents.Normalized;

namespace MeshBoard.Collector.GraphProjector.Services;

public interface IGraphNodeProjectionService
{
    Task ProjectAsync(NodeObserved node, CancellationToken ct = default);
}
