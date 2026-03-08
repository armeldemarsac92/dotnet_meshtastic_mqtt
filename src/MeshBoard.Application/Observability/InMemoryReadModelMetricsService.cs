using System.Collections.Concurrent;
using System.Threading;
using MeshBoard.Contracts.Diagnostics;

namespace MeshBoard.Application.Observability;

public sealed class InMemoryReadModelMetricsService : IReadModelMetricsService
{
    private readonly ConcurrentDictionary<ReadModelMetricKind, MetricState> _metrics = new();

    public IReadOnlyCollection<ReadModelMetricSnapshot> GetSnapshots()
    {
        return Enum.GetValues<ReadModelMetricKind>()
            .Select(kind => _metrics.GetOrAdd(kind, static _ => new MetricState()).ToSnapshot(kind))
            .ToArray();
    }

    public void RecordCacheHit(ReadModelMetricKind kind)
    {
        _metrics.GetOrAdd(kind, static _ => new MetricState()).RecordCacheHit();
    }

    public void RecordCacheMiss(ReadModelMetricKind kind, bool forcedRefresh)
    {
        _metrics.GetOrAdd(kind, static _ => new MetricState()).RecordCacheMiss(forcedRefresh);
    }

    public void RecordLoadDuration(ReadModelMetricKind kind, TimeSpan duration)
    {
        _metrics.GetOrAdd(kind, static _ => new MetricState()).RecordLoadDuration(duration);
    }

    private sealed class MetricState
    {
        private long _cacheHitCount;
        private long _cacheMissCount;
        private long _forcedRefreshCount;
        private long _lastLoadMilliseconds;
        private long _loadCount;
        private long _maxLoadMilliseconds;
        private long _totalLoadMilliseconds;

        public void RecordCacheHit()
        {
            Interlocked.Increment(ref _cacheHitCount);
        }

        public void RecordCacheMiss(bool forcedRefresh)
        {
            Interlocked.Increment(ref _cacheMissCount);

            if (forcedRefresh)
            {
                Interlocked.Increment(ref _forcedRefreshCount);
            }
        }

        public void RecordLoadDuration(TimeSpan duration)
        {
            var elapsedMilliseconds = Math.Max(0L, (long)Math.Round(duration.TotalMilliseconds, MidpointRounding.AwayFromZero));

            Interlocked.Increment(ref _loadCount);
            Interlocked.Add(ref _totalLoadMilliseconds, elapsedMilliseconds);
            Interlocked.Exchange(ref _lastLoadMilliseconds, elapsedMilliseconds);

            while (true)
            {
                var currentMax = Volatile.Read(ref _maxLoadMilliseconds);
                if (currentMax >= elapsedMilliseconds)
                {
                    break;
                }

                if (Interlocked.CompareExchange(ref _maxLoadMilliseconds, elapsedMilliseconds, currentMax) == currentMax)
                {
                    break;
                }
            }
        }

        public ReadModelMetricSnapshot ToSnapshot(ReadModelMetricKind kind)
        {
            var loadCount = Volatile.Read(ref _loadCount);
            var totalLoadMilliseconds = Volatile.Read(ref _totalLoadMilliseconds);

            return new ReadModelMetricSnapshot
            {
                Kind = kind,
                CacheHitCount = Volatile.Read(ref _cacheHitCount),
                CacheMissCount = Volatile.Read(ref _cacheMissCount),
                ForcedRefreshCount = Volatile.Read(ref _forcedRefreshCount),
                LoadCount = loadCount,
                AverageLoadMilliseconds = loadCount == 0 ? 0 : (double)totalLoadMilliseconds / loadCount,
                MaxLoadMilliseconds = Volatile.Read(ref _maxLoadMilliseconds),
                LastLoadMilliseconds = Volatile.Read(ref _lastLoadMilliseconds)
            };
        }
    }
}
