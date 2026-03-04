using MeshBoard.Application.Abstractions.Persistence;
using MeshBoard.Contracts.Nodes;
using MeshBoard.Infrastructure.Persistence.Context;
using MeshBoard.Infrastructure.Persistence.Mapping;
using MeshBoard.Infrastructure.Persistence.SQL;
using MeshBoard.Infrastructure.Persistence.SQL.Responses;
using Microsoft.Extensions.Logging;

namespace MeshBoard.Infrastructure.Persistence.Repositories;

internal sealed class NodeRepository : INodeRepository
{
    private readonly IDbContext _dbContext;
    private readonly ILogger<NodeRepository> _logger;

    public NodeRepository(IDbContext dbContext, ILogger<NodeRepository> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<IReadOnlyCollection<NodeSummary>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Attempting to fetch nodes");

        var sqlResponses = await _dbContext.QueryAsync<NodeSqlResponse>(
            NodeQueries.GetNodes,
            cancellationToken: cancellationToken);

        return sqlResponses.MapToNodes();
    }

    public async Task UpsertAsync(UpsertObservedNodeRequest request, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Attempting to upsert observed node: {NodeId}", request.NodeId);

        await _dbContext.ExecuteAsync(
            NodeQueries.UpsertNode,
            request.ToSqlRequest(),
            cancellationToken);
    }
}
