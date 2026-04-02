namespace MeshBoard.Infrastructure.Neo4j.Persistence;

public interface IGraphTopologyRepository
{
    Task UpsertNodeAsync(GraphNodeWriteRequest request, CancellationToken ct = default);

    Task UpsertLinkAsync(GraphLinkWriteRequest request, CancellationToken ct = default);
}
