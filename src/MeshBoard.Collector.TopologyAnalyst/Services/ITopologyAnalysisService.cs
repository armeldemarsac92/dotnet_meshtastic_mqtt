namespace MeshBoard.Collector.TopologyAnalyst.Services;

public interface ITopologyAnalysisService
{
    Task RunAnalysisAsync(CancellationToken cancellationToken = default);
}
