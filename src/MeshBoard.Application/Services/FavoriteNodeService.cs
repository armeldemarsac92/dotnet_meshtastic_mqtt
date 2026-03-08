using MeshBoard.Application.Abstractions.Persistence;
using MeshBoard.Application.Abstractions.Workspaces;
using MeshBoard.Contracts.Exceptions;
using MeshBoard.Contracts.Favorites;
using MeshBoard.Contracts.Realtime;
using Microsoft.Extensions.Logging;

namespace MeshBoard.Application.Services;

public interface IFavoriteNodeService
{
    Task<IReadOnlyCollection<FavoriteNode>> GetFavoriteNodes(CancellationToken cancellationToken = default);

    Task<FavoriteNode> SaveFavoriteNode(SaveFavoriteNodeRequest request, CancellationToken cancellationToken = default);

    Task RemoveFavoriteNode(string nodeId, CancellationToken cancellationToken = default);
}

public sealed class FavoriteNodeService : IFavoriteNodeService
{
    private readonly IFavoriteNodeRepository _favoriteNodeRepository;
    private readonly ILogger<FavoriteNodeService> _logger;
    private readonly IProjectionChangeRepository _projectionChangeRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IWorkspaceContextAccessor _workspaceContextAccessor;

    public FavoriteNodeService(
        IFavoriteNodeRepository favoriteNodeRepository,
        IProjectionChangeRepository projectionChangeRepository,
        IUnitOfWork unitOfWork,
        IWorkspaceContextAccessor workspaceContextAccessor,
        ILogger<FavoriteNodeService> logger)
    {
        _favoriteNodeRepository = favoriteNodeRepository;
        _projectionChangeRepository = projectionChangeRepository;
        _unitOfWork = unitOfWork;
        _workspaceContextAccessor = workspaceContextAccessor;
        _logger = logger;
    }

    public async Task<IReadOnlyCollection<FavoriteNode>> GetFavoriteNodes(CancellationToken cancellationToken = default)
    {
        var workspaceId = _workspaceContextAccessor.GetWorkspaceId();
        _logger.LogDebug("Attempting to get favorite nodes");

        var favoriteNodes = await _favoriteNodeRepository.GetAllAsync(workspaceId, cancellationToken);

        _logger.LogDebug("Retrieved {FavoriteNodeCount} favorite nodes", favoriteNodes.Count);

        return favoriteNodes;
    }

    public async Task<FavoriteNode> SaveFavoriteNode(
        SaveFavoriteNodeRequest request,
        CancellationToken cancellationToken = default)
    {
        var workspaceId = _workspaceContextAccessor.GetWorkspaceId();
        _logger.LogInformation("Attempting to save favorite node: {NodeId}", request.NodeId);

        await _unitOfWork.BeginTransactionAsync(cancellationToken);

        try
        {
            var favoriteNode = await _favoriteNodeRepository.UpsertAsync(workspaceId, request, cancellationToken);

            await _unitOfWork.CommitAsync(cancellationToken);
            await PublishFavoritesChangedAsync(workspaceId, favoriteNode.NodeId, cancellationToken);

            _logger.LogInformation("Saved favorite node: {NodeId}", favoriteNode.NodeId);

            return favoriteNode;
        }
        catch
        {
            await _unitOfWork.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task RemoveFavoriteNode(string nodeId, CancellationToken cancellationToken = default)
    {
        var workspaceId = _workspaceContextAccessor.GetWorkspaceId();
        _logger.LogInformation("Attempting to remove favorite node: {NodeId}", nodeId);

        await _unitOfWork.BeginTransactionAsync(cancellationToken);

        try
        {
            var deleted = await _favoriteNodeRepository.DeleteAsync(workspaceId, nodeId, cancellationToken);

            if (!deleted)
            {
                throw new NotFoundException($"Favorite node not found for node ID '{nodeId}'.");
            }

            await _unitOfWork.CommitAsync(cancellationToken);
            await PublishFavoritesChangedAsync(workspaceId, nodeId, cancellationToken);

            _logger.LogInformation("Removed favorite node: {NodeId}", nodeId);
        }
        catch
        {
            await _unitOfWork.RollbackAsync(cancellationToken);
            throw;
        }
    }

    private async Task PublishFavoritesChangedAsync(
        string workspaceId,
        string nodeId,
        CancellationToken cancellationToken)
    {
        try
        {
            await _projectionChangeRepository.AppendAsync(
                workspaceId,
                [
                    new ProjectionChangeDescriptor
                    {
                        Kind = ProjectionChangeKind.FavoriteNodesChanged,
                        EntityKey = nodeId.Trim()
                    }
                ],
                cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogWarning(
                exception,
                "Failed to persist favorite-node projection change for workspace {WorkspaceId} and node {NodeId}",
                workspaceId,
                nodeId);
        }
    }
}
