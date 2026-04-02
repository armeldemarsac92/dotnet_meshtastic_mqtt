namespace MeshBoard.Infrastructure.Persistence.Repositories.Neo4j;

public interface IGraphTopologyRepository
{
    Task UpsertNodeAsync(GraphNodeWriteRequest request, CancellationToken ct = default);

    Task UpsertLinkAsync(GraphLinkWriteRequest request, CancellationToken ct = default);
}
