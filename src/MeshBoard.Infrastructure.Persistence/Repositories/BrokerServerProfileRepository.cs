using MeshBoard.Application.Abstractions.Persistence;
using MeshBoard.Contracts.Configuration;
using MeshBoard.Infrastructure.Persistence.Context;
using MeshBoard.Infrastructure.Persistence.Mapping;
using MeshBoard.Infrastructure.Persistence.SQL;
using MeshBoard.Infrastructure.Persistence.SQL.Responses;
using Microsoft.Extensions.Logging;

namespace MeshBoard.Infrastructure.Persistence.Repositories;

internal sealed class BrokerServerProfileRepository : IBrokerServerProfileRepository
{
    private readonly IDbContext _dbContext;
    private readonly ILogger<BrokerServerProfileRepository> _logger;

    public BrokerServerProfileRepository(IDbContext dbContext, ILogger<BrokerServerProfileRepository> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<IReadOnlyCollection<WorkspaceBrokerServerProfile>> GetAllActiveAsync(
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Attempting to fetch active broker server profiles across all workspaces");

        var responses = await _dbContext.QueryAsync<BrokerServerProfileSqlResponse>(
            BrokerServerProfileQueries.GetAllActiveAcrossWorkspaces,
            cancellationToken: cancellationToken);

        return responses
            .Select(response => response.MapToWorkspaceBrokerServerProfile())
            .ToList();
    }

    public async Task<IReadOnlyCollection<BrokerServerProfile>> GetAllAsync(
        string workspaceId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Attempting to fetch broker server profiles for workspace {WorkspaceId}", workspaceId);

        var responses = await _dbContext.QueryAsync<BrokerServerProfileSqlResponse>(
            BrokerServerProfileQueries.GetAll,
            new { WorkspaceId = workspaceId },
            cancellationToken: cancellationToken);

        return responses.Select(response => response.MapToBrokerServerProfile()).ToList();
    }

    public async Task<BrokerServerProfile?> GetActiveAsync(
        string workspaceId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Attempting to fetch active broker server profile for workspace {WorkspaceId}", workspaceId);

        var response = await _dbContext.QueryFirstOrDefaultAsync<BrokerServerProfileSqlResponse>(
            BrokerServerProfileQueries.GetActive,
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
            "Attempting to fetch broker server profile by id: {ProfileId} for workspace {WorkspaceId}",
            id,
            workspaceId);

        var response = await _dbContext.QueryFirstOrDefaultAsync<BrokerServerProfileSqlResponse>(
            BrokerServerProfileQueries.GetById,
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
        _logger.LogDebug(
            "Attempting to set the exclusive active broker server profile: {ProfileId} for workspace {WorkspaceId}",
            id,
            workspaceId);

        return _dbContext.ExecuteAsync(
            BrokerServerProfileQueries.SetExclusiveActive,
            new
            {
                WorkspaceId = workspaceId,
                Id = id.ToString()
            },
            cancellationToken);
    }

    public async Task<bool> AreSubscriptionIntentsInitializedAsync(
        string workspaceId,
        Guid id,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug(
            "Attempting to read subscription intent initialization state for profile {ProfileId} in workspace {WorkspaceId}",
            id,
            workspaceId);

        var initialized = await _dbContext.QueryFirstOrDefaultAsync<int>(
            BrokerServerProfileQueries.GetSubscriptionIntentsInitialized,
            new
            {
                WorkspaceId = workspaceId,
                Id = id.ToString()
            },
            cancellationToken);

        return initialized == 1;
    }

    public Task MarkSubscriptionIntentsInitializedAsync(
        string workspaceId,
        Guid id,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug(
            "Attempting to mark subscription intents initialized for profile {ProfileId} in workspace {WorkspaceId}",
            id,
            workspaceId);

        return _dbContext.ExecuteAsync(
            BrokerServerProfileQueries.MarkSubscriptionIntentsInitialized,
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
            "Attempting to upsert broker server profile {Name} ({Host}:{Port}) for workspace {WorkspaceId}",
            request.Name,
            request.Host,
            request.Port,
            workspaceId);

        var sqlRequest = request.ToSqlRequest(workspaceId);

        await _dbContext.ExecuteAsync(
            BrokerServerProfileQueries.Upsert,
            sqlRequest,
            cancellationToken);

        var response = await _dbContext.QueryFirstOrDefaultAsync<BrokerServerProfileSqlResponse>(
            BrokerServerProfileQueries.GetById,
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
