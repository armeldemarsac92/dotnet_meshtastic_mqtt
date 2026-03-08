using System.Collections.Concurrent;

namespace MeshBoard.Application.Caching;

public sealed class InMemoryReadModelCacheInvalidator : IReadModelCacheInvalidator
{
    private readonly ConcurrentDictionary<string, long> _versions = new(StringComparer.Ordinal);

    public long GetStamp(string workspaceId, ReadModelCacheRegion region)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceId);

        return _versions.GetOrAdd(CreateKey(workspaceId, region), 0);
    }

    public void Invalidate(string workspaceId, params ReadModelCacheRegion[] regions)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceId);
        ArgumentNullException.ThrowIfNull(regions);

        foreach (var region in regions.Distinct())
        {
            _versions.AddOrUpdate(
                CreateKey(workspaceId, region),
                1,
                static (_, currentValue) => currentValue + 1);
        }
    }

    private static string CreateKey(string workspaceId, ReadModelCacheRegion region)
    {
        return $"{workspaceId.Trim()}::{region}";
    }
}
