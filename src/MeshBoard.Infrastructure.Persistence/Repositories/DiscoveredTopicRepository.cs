using MeshBoard.Application.Abstractions.Persistence;
using MeshBoard.Contracts.Topics;
using MeshBoard.Infrastructure.Persistence.Context;
using MeshBoard.Infrastructure.Persistence.SQL;
using MeshBoard.Infrastructure.Persistence.SQL.Requests;
using MeshBoard.Infrastructure.Persistence.SQL.Responses;
using Microsoft.Extensions.Logging;

namespace MeshBoard.Infrastructure.Persistence.Repositories;

internal sealed class DiscoveredTopicRepository : IDiscoveredTopicRepository
{
    private readonly IDbContext _dbContext;
    private readonly ILogger<DiscoveredTopicRepository> _logger;

    public DiscoveredTopicRepository(IDbContext dbContext, ILogger<DiscoveredTopicRepository> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<IReadOnlyCollection<TopicCatalogEntry>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Attempting to fetch discovered topics");

        var sqlResponses = await _dbContext.QueryAsync<DiscoveredTopicSqlResponse>(
            DiscoveredTopicQueries.GetDiscoveredTopics,
            cancellationToken: cancellationToken);

        return sqlResponses
            .Select(
                response => new TopicCatalogEntry
                {
                    Label = $"{response.Region} · {response.Channel}",
                    TopicPattern = response.TopicPattern,
                    Region = response.Region,
                    Channel = response.Channel,
                    IsRecommended = false
                })
            .ToList();
    }

    public async Task UpsertAsync(
        string topicPattern,
        string region,
        string channel,
        DateTimeOffset observedAtUtc,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Attempting to upsert discovered topic {TopicPattern}", topicPattern);

        await _dbContext.ExecuteAsync(
            DiscoveredTopicQueries.UpsertDiscoveredTopic,
            new UpsertDiscoveredTopicSqlRequest
            {
                TopicPattern = topicPattern,
                Region = region,
                Channel = channel,
                ObservedAtUtc = observedAtUtc.ToString("O")
            },
            cancellationToken);
    }
}
