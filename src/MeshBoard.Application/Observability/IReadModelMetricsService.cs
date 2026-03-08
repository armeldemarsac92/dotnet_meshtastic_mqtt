using MeshBoard.Contracts.Diagnostics;

namespace MeshBoard.Application.Observability;

public interface IReadModelMetricsService
{
    IReadOnlyCollection<ReadModelMetricSnapshot> GetSnapshots();

    void RecordCacheHit(ReadModelMetricKind kind);

    void RecordCacheMiss(ReadModelMetricKind kind, bool forcedRefresh);

    void RecordLoadDuration(ReadModelMetricKind kind, TimeSpan duration);
}
