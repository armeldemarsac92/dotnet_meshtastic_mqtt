using MeshBoard.Contracts.Diagnostics;

namespace MeshBoard.Application.Observability;

public interface IActiveCircuitMetricsService
{
    ActiveCircuitMetricSnapshot GetSnapshot();

    void RecordCircuitClosed();

    void RecordCircuitOpened();
}
