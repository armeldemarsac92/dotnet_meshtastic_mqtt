namespace MeshBoard.Contracts.Diagnostics;

public sealed class ReadModelMetricSnapshot
{
    public ReadModelMetricKind Kind { get; set; }

    public long CacheHitCount { get; set; }

    public long CacheMissCount { get; set; }

    public long ForcedRefreshCount { get; set; }

    public long LoadCount { get; set; }

    public double AverageLoadMilliseconds { get; set; }

    public long MaxLoadMilliseconds { get; set; }

    public long LastLoadMilliseconds { get; set; }
}
