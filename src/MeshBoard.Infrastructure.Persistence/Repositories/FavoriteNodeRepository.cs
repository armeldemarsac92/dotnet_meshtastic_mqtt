using MeshBoard.Application.Abstractions.Persistence;
using MeshBoard.Contracts.Favorites;
using MeshBoard.Infrastructure.Persistence.Context;
using MeshBoard.Infrastructure.Persistence.Mapping;
using MeshBoard.Infrastructure.Persistence.SQL;
using MeshBoard.Infrastructure.Persistence.SQL.Responses;
using Microsoft.Extensions.Logging;

namespace MeshBoard.Infrastructure.Persistence.Repositories;

internal sealed class FavoriteNodeRepository : IFavoriteNodeRepository
{
    private readonly IDbContext _dbContext;
    private readonly ILogger<FavoriteNodeRepository> _logger;

    public FavoriteNodeRepository(IDbContext dbContext, ILogger<FavoriteNodeRepository> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<IReadOnlyCollection<FavoriteNode>> GetAllAsync(
        string workspaceId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Attempting to fetch favorite nodes for workspace {WorkspaceId}", workspaceId);

        var favoriteNodes = await _dbContext.QueryAsync<FavoriteNodeSqlResponse>(
            FavoriteNodeQueries.GetFavoriteNodes,
            new { WorkspaceId = workspaceId },
            cancellationToken: cancellationToken);

        return favoriteNodes.Select(x => x.MapToFavoriteNode()).ToList();
    }

    public async Task<FavoriteNode> UpsertAsync(
        string workspaceId,
        SaveFavoriteNodeRequest request,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Attempting to upsert favorite node: {NodeId} for workspace {WorkspaceId}",
            request.NodeId,
            workspaceId);

        var sqlRequest = request.ToSqlRequest(workspaceId);

        await _dbContext.ExecuteAsync(
            FavoriteNodeQueries.UpsertFavoriteNode,
            sqlRequest,
            cancellationToken);

        var favoriteNode = await _dbContext.QueryFirstOrDefaultAsync<FavoriteNodeSqlResponse>(
            FavoriteNodeQueries.GetFavoriteNodeByNodeId,
            new
            {
                WorkspaceId = workspaceId,
                sqlRequest.NodeId
            },
            cancellationToken);

        if (favoriteNode is null)
        {
            throw new InvalidOperationException($"Failed to read back favorite node '{sqlRequest.NodeId}' after upsert.");
        }

        return favoriteNode.MapToFavoriteNode();
    }

    public async Task<bool> DeleteAsync(
        string workspaceId,
        string nodeId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Attempting to delete favorite node: {NodeId} for workspace {WorkspaceId}",
            nodeId,
            workspaceId);

        var affectedRows = await _dbContext.ExecuteAsync(
            FavoriteNodeQueries.DeleteFavoriteNode,
            new
            {
                WorkspaceId = workspaceId,
                NodeId = nodeId
            },
            cancellationToken);

        return affectedRows > 0;
    }
}
