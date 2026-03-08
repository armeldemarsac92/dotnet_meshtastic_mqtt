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

    public async Task<IReadOnlyCollection<TopicCatalogEntry>> GetAllAsync(
        string brokerServer,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Attempting to fetch discovered topics for broker {BrokerServer}", brokerServer);

        var sqlResponses = await _dbContext.QueryAsync<DiscoveredTopicSqlResponse>(
            DiscoveredTopicQueries.GetDiscoveredTopics,
            new
            {
                BrokerServer = brokerServer
            },
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
        string brokerServer,
        string topicPattern,
        string region,
        string channel,
        DateTimeOffset observedAtUtc,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug(
            "Attempting to upsert discovered topic {TopicPattern} for broker {BrokerServer}",
            topicPattern,
            brokerServer);

        await _dbContext.ExecuteAsync(
            DiscoveredTopicQueries.UpsertDiscoveredTopic,
            new UpsertDiscoveredTopicSqlRequest
            {
                BrokerServer = brokerServer,
                TopicPattern = topicPattern,
                Region = region,
                Channel = channel,
                ObservedAtUtc = observedAtUtc.ToString("O")
            },
            cancellationToken);
    }
}
