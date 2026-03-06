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

    public async Task<IReadOnlyCollection<BrokerServerProfile>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Attempting to fetch broker server profiles");

        var responses = await _dbContext.QueryAsync<BrokerServerProfileSqlResponse>(
            BrokerServerProfileQueries.GetAll,
            cancellationToken: cancellationToken);

        return responses.Select(response => response.MapToBrokerServerProfile()).ToList();
    }

    public async Task<BrokerServerProfile?> GetActiveAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Attempting to fetch active broker server profile");

        var response = await _dbContext.QueryFirstOrDefaultAsync<BrokerServerProfileSqlResponse>(
            BrokerServerProfileQueries.GetActive,
            cancellationToken: cancellationToken);

        return response?.MapToBrokerServerProfile();
    }

    public async Task<BrokerServerProfile?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Attempting to fetch broker server profile by id: {ProfileId}", id);

        var response = await _dbContext.QueryFirstOrDefaultAsync<BrokerServerProfileSqlResponse>(
            BrokerServerProfileQueries.GetById,
            new { Id = id.ToString() },
            cancellationToken);

        return response?.MapToBrokerServerProfile();
    }

    public Task ClearActiveAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Attempting to clear active broker server profile");

        return _dbContext.ExecuteAsync(BrokerServerProfileQueries.ClearActive, cancellationToken: cancellationToken);
    }

    public Task SetActiveAsync(Guid id, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Attempting to set active broker server profile: {ProfileId}", id);

        return _dbContext.ExecuteAsync(
            BrokerServerProfileQueries.SetActive,
            new { Id = id.ToString() },
            cancellationToken);
    }

    public async Task<BrokerServerProfile> UpsertAsync(
        SaveBrokerServerProfileRequest request,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Attempting to upsert broker server profile {Name} ({Host}:{Port})",
            request.Name,
            request.Host,
            request.Port);

        var sqlRequest = request.ToSqlRequest();

        await _dbContext.ExecuteAsync(
            BrokerServerProfileQueries.Upsert,
            sqlRequest,
            cancellationToken);

        var response = await _dbContext.QueryFirstOrDefaultAsync<BrokerServerProfileSqlResponse>(
            BrokerServerProfileQueries.GetById,
            new { Id = sqlRequest.Id },
            cancellationToken);

        if (response is null)
        {
            throw new InvalidOperationException($"Failed to read back broker server profile '{sqlRequest.Id}' after upsert.");
        }

        return response.MapToBrokerServerProfile();
    }
}
