using MeshBoard.Application.Abstractions.Persistence;
using MeshBoard.Contracts.Topics;
using MeshBoard.Infrastructure.Persistence.Context;
using MeshBoard.Infrastructure.Persistence.Mapping;
using MeshBoard.Infrastructure.Persistence.SQL;
using MeshBoard.Infrastructure.Persistence.SQL.Responses;
using Microsoft.Extensions.Logging;

namespace MeshBoard.Infrastructure.Persistence.Repositories.Collector;

internal sealed class CollectorDiscoveredTopicRepository : IDiscoveredTopicRepository
{
    private readonly CollectorChannelResolver _channelResolver;
    private readonly IDbContext _dbContext;
    private readonly ILogger<CollectorDiscoveredTopicRepository> _logger;

    public CollectorDiscoveredTopicRepository(
        IDbContext dbContext,
        CollectorChannelResolver channelResolver,
        ILogger<CollectorDiscoveredTopicRepository> logger)
    {
        _dbContext = dbContext;
        _channelResolver = channelResolver;
        _logger = logger;
    }

    public async Task<IReadOnlyCollection<TopicCatalogEntry>> GetAllAsync(
        string workspaceId,
        string brokerServer,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug(
            "Attempting to fetch collector discovered topics for broker {BrokerServer}",
            brokerServer);

        var responses = await _dbContext.QueryAsync<DiscoveredTopicSqlResponse>(
            CollectorChannelQueries.GetDiscoveredTopics,
            new
            {
                BrokerServer = string.IsNullOrWhiteSpace(brokerServer) ? "unknown" : brokerServer.Trim()
            },
            cancellationToken);

        return responses.MapToTopicCatalogEntries();
    }

    public async Task UpsertAsync(
        string workspaceId,
        string brokerServer,
        string topicPattern,
        string region,
        string channel,
        DateTimeOffset observedAtUtc,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug(
            "Attempting to upsert collector discovered topic {TopicPattern} for broker {BrokerServer}",
            topicPattern,
            brokerServer);

        await _channelResolver.ResolveDiscoveredTopicAsync(
            brokerServer,
            topicPattern,
            region,
            channel,
            observedAtUtc,
            cancellationToken);
    }
}
