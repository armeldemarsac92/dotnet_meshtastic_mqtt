using MeshBoard.Application.Abstractions.Persistence;
using MeshBoard.Contracts.Meshtastic;
using MeshBoard.Infrastructure.Persistence.Context;
using MeshBoard.Infrastructure.Persistence.Mapping;
using MeshBoard.Infrastructure.Persistence.SQL;
using MeshBoard.Infrastructure.Persistence.SQL.Responses;
using Microsoft.Extensions.Logging;

namespace MeshBoard.Infrastructure.Persistence.Repositories;

internal sealed class SubscriptionIntentRepository : ISubscriptionIntentRepository
{
    private readonly IDbContext _dbContext;
    private readonly ILogger<SubscriptionIntentRepository> _logger;

    public SubscriptionIntentRepository(IDbContext dbContext, ILogger<SubscriptionIntentRepository> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<IReadOnlyCollection<SubscriptionIntent>> GetAllAsync(
        string workspaceId,
        Guid brokerServerProfileId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug(
            "Attempting to fetch subscription intents for workspace {WorkspaceId} and profile {ProfileId}",
            workspaceId,
            brokerServerProfileId);

        var responses = await _dbContext.QueryAsync<SubscriptionIntentSqlResponse>(
            SubscriptionIntentQueries.GetSubscriptionIntents,
            new
            {
                WorkspaceId = workspaceId,
                BrokerServerProfileId = brokerServerProfileId.ToString()
            },
            cancellationToken);

        return responses.Select(response => response.MapToSubscriptionIntent()).ToList();
    }

    public async Task<bool> AddAsync(
        string workspaceId,
        Guid brokerServerProfileId,
        string topicFilter,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Attempting to add subscription intent {TopicFilter} for workspace {WorkspaceId} and profile {ProfileId}",
            topicFilter,
            workspaceId,
            brokerServerProfileId);

        var affectedRows = await _dbContext.ExecuteAsync(
            SubscriptionIntentQueries.InsertSubscriptionIntent,
            new
            {
                WorkspaceId = workspaceId,
                BrokerServerProfileId = brokerServerProfileId.ToString(),
                TopicFilter = topicFilter,
                CreatedAtUtc = DateTimeOffset.UtcNow.ToString("O")
            },
            cancellationToken);

        return affectedRows > 0;
    }

    public async Task<bool> DeleteAsync(
        string workspaceId,
        Guid brokerServerProfileId,
        string topicFilter,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Attempting to delete subscription intent {TopicFilter} for workspace {WorkspaceId} and profile {ProfileId}",
            topicFilter,
            workspaceId,
            brokerServerProfileId);

        var affectedRows = await _dbContext.ExecuteAsync(
            SubscriptionIntentQueries.DeleteSubscriptionIntent,
            new
            {
                WorkspaceId = workspaceId,
                BrokerServerProfileId = brokerServerProfileId.ToString(),
                TopicFilter = topicFilter
            },
            cancellationToken);

        return affectedRows > 0;
    }
}
