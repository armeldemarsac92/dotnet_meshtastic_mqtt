using MeshBoard.Application.Abstractions.Persistence;
using MeshBoard.Contracts.Exceptions;
using MeshBoard.Contracts.Favorites;
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
    private readonly IUnitOfWork _unitOfWork;

    public FavoriteNodeService(
        IFavoriteNodeRepository favoriteNodeRepository,
        IUnitOfWork unitOfWork,
        ILogger<FavoriteNodeService> logger)
    {
        _favoriteNodeRepository = favoriteNodeRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<IReadOnlyCollection<FavoriteNode>> GetFavoriteNodes(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Attempting to get favorite nodes");

        var favoriteNodes = await _favoriteNodeRepository.GetAllAsync(cancellationToken);

        _logger.LogInformation("Retrieved {FavoriteNodeCount} favorite nodes", favoriteNodes.Count);

        return favoriteNodes;
    }

    public async Task<FavoriteNode> SaveFavoriteNode(
        SaveFavoriteNodeRequest request,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Attempting to save favorite node: {NodeId}", request.NodeId);

        await _unitOfWork.BeginTransactionAsync(cancellationToken);

        try
        {
            var favoriteNode = await _favoriteNodeRepository.UpsertAsync(request, cancellationToken);

            await _unitOfWork.CommitAsync(cancellationToken);

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
        _logger.LogInformation("Attempting to remove favorite node: {NodeId}", nodeId);

        await _unitOfWork.BeginTransactionAsync(cancellationToken);

        try
        {
            var deleted = await _favoriteNodeRepository.DeleteAsync(nodeId, cancellationToken);

            if (!deleted)
            {
                throw new NotFoundException($"Favorite node not found for node ID '{nodeId}'.");
            }

            await _unitOfWork.CommitAsync(cancellationToken);

            _logger.LogInformation("Removed favorite node: {NodeId}", nodeId);
        }
        catch
        {
            await _unitOfWork.RollbackAsync(cancellationToken);
            throw;
        }
    }
}
