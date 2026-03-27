using MeshBoard.Contracts.CollectorEvents.Normalized;

namespace MeshBoard.Collector.GraphProjector.Services;

public interface IGraphLinkProjectionService
{
    Task ProjectAsync(LinkObserved link, CancellationToken ct = default);
}
