using MeshBoard.Contracts.CollectorEvents.Normalized;

namespace MeshBoard.Collector.StatsProjector.Services;

public interface IStatsTelemetryProjectionService
{
    Task ProjectAsync(TelemetryObserved telemetry, CancellationToken ct = default);
}
