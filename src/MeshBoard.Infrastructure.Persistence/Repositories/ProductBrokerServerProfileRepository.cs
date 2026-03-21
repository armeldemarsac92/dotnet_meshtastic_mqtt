using MeshBoard.Application.Abstractions.Persistence;
using MeshBoard.Contracts.Configuration;
using MeshBoard.Infrastructure.Persistence.Context;
using MeshBoard.Infrastructure.Persistence.Mapping;
using MeshBoard.Infrastructure.Persistence.SQL;
using MeshBoard.Infrastructure.Persistence.SQL.Responses;
using Microsoft.Extensions.Logging;

namespace MeshBoard.Infrastructure.Persistence.Repositories;

internal sealed class ProductBrokerServerProfileRepository : IBrokerServerProfileRepository
{
    private readonly IDbContext _dbContext;
    private readonly ILogger<ProductBrokerServerProfileRepository> _logger;

    public ProductBrokerServerProfileRepository(
        IDbContext dbContext,
        ILogger<ProductBrokerServerProfileRepository> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<IReadOnlyCollection<WorkspaceBrokerServerProfile>> GetAllActiveAsync(
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Attempting to fetch product-path active broker server profiles across all workspaces");

        var responses = await _dbContext.QueryAsync<BrokerServerProfileSqlResponse>(
            ProductBrokerServerProfileQueries.GetAllActiveAcrossWorkspaces,
            cancellationToken: cancellationToken);

        return responses.Select(response => response.MapToWorkspaceBrokerServerProfile()).ToList();
    }

    public async Task<IReadOnlyCollection<WorkspaceBrokerServerProfile>> GetAllActiveUserOwnedAsync(
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Attempting to fetch product-path active broker server profiles across user-owned workspaces");

        var responses = await _dbContext.QueryAsync<BrokerServerProfileSqlResponse>(
            ProductBrokerServerProfileQueries.GetAllActiveAcrossUserOwnedWorkspaces,
            cancellationToken: cancellationToken);

        return responses.Select(response => response.MapToWorkspaceBrokerServerProfile()).ToList();
    }

    public async Task<IReadOnlyCollection<BrokerServerProfile>> GetAllAsync(
        string workspaceId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug(
            "Attempting to fetch product-path broker server profiles for workspace {WorkspaceId}",
            workspaceId);

        var responses = await _dbContext.QueryAsync<BrokerServerProfileSqlResponse>(
            ProductBrokerServerProfileQueries.GetAll,
            new { WorkspaceId = workspaceId },
            cancellationToken: cancellationToken);

        return responses.Select(response => response.MapToBrokerServerProfile()).ToList();
    }

    public async Task<BrokerServerProfile?> GetActiveAsync(
        string workspaceId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug(
            "Attempting to fetch product-path active broker server profile for workspace {WorkspaceId}",
            workspaceId);

        var response = await _dbContext.QueryFirstOrDefaultAsync<BrokerServerProfileSqlResponse>(
            ProductBrokerServerProfileQueries.GetActive,
            new { WorkspaceId = workspaceId },
            cancellationToken: cancellationToken);

        return response?.MapToBrokerServerProfile();
    }

    public async Task<BrokerServerProfile?> GetByIdAsync(
        string workspaceId,
        Guid id,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug(
            "Attempting to fetch product-path broker server profile by id: {ProfileId} for workspace {WorkspaceId}",
            id,
            workspaceId);

        var response = await _dbContext.QueryFirstOrDefaultAsync<BrokerServerProfileSqlResponse>(
            ProductBrokerServerProfileQueries.GetById,
            new
            {
                WorkspaceId = workspaceId,
                Id = id.ToString()
            },
            cancellationToken);

        return response?.MapToBrokerServerProfile();
    }

    public Task SetExclusiveActiveAsync(
        string workspaceId,
        Guid id,
        CancellationToken cancellationToken = default)
    {
        return _dbContext.ExecuteAsync(
            ProductBrokerServerProfileQueries.SetExclusiveActive,
            new
            {
                WorkspaceId = workspaceId,
                Id = id.ToString()
            },
            cancellationToken);
    }

    public async Task<BrokerServerProfile> UpsertAsync(
        string workspaceId,
        SaveBrokerServerProfileRequest request,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Attempting to upsert product-path broker server profile {Name} ({Host}:{Port}) for workspace {WorkspaceId}",
            request.Name,
            request.Host,
            request.Port,
            workspaceId);

        var sqlRequest = request.ToSqlRequest(workspaceId);

        await _dbContext.ExecuteAsync(
            ProductBrokerServerProfileQueries.Upsert,
            sqlRequest,
            cancellationToken);

        var response = await _dbContext.QueryFirstOrDefaultAsync<BrokerServerProfileSqlResponse>(
            ProductBrokerServerProfileQueries.GetById,
            new
            {
                WorkspaceId = workspaceId,
                Id = sqlRequest.Id
            },
            cancellationToken);

        if (response is null)
        {
            throw new InvalidOperationException($"Failed to read back broker server profile '{sqlRequest.Id}' after upsert.");
        }

        return response.MapToBrokerServerProfile();
    }
}
