namespace MeshBoard.Application.Caching;

public interface IReadModelCacheInvalidator
{
    long GetStamp(string workspaceId, ReadModelCacheRegion region);

    void Invalidate(string workspaceId, params ReadModelCacheRegion[] regions);
}
