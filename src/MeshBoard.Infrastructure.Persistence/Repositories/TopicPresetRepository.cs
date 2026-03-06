using MeshBoard.Application.Abstractions.Persistence;
using MeshBoard.Contracts.Topics;
using MeshBoard.Infrastructure.Persistence.Context;
using MeshBoard.Infrastructure.Persistence.Mapping;
using MeshBoard.Infrastructure.Persistence.SQL;
using MeshBoard.Infrastructure.Persistence.SQL.Responses;
using Microsoft.Extensions.Logging;

namespace MeshBoard.Infrastructure.Persistence.Repositories;

internal sealed class TopicPresetRepository : ITopicPresetRepository
{
    private readonly IDbContext _dbContext;
    private readonly ILogger<TopicPresetRepository> _logger;

    public TopicPresetRepository(IDbContext dbContext, ILogger<TopicPresetRepository> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<IReadOnlyCollection<TopicPreset>> GetAllAsync(
        string brokerServer,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Attempting to fetch topic presets for broker {BrokerServer}", brokerServer);

        var topicPresets = await _dbContext.QueryAsync<TopicPresetSqlResponse>(
            TopicPresetQueries.GetTopicPresets,
            new
            {
                BrokerServer = brokerServer
            },
            cancellationToken: cancellationToken);

        return topicPresets.Select(x => x.MapToTopicPreset()).ToList();
    }

    public async Task<TopicPreset> UpsertAsync(
        string brokerServer,
        SaveTopicPresetRequest request,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Attempting to upsert topic preset: {TopicPattern} for broker {BrokerServer}",
            request.TopicPattern,
            brokerServer);

        var sqlRequest = request.ToSqlRequest(brokerServer);

        if (sqlRequest.IsDefault == 1)
        {
            await _dbContext.ExecuteAsync(
                TopicPresetQueries.ClearDefaultTopicPresets,
                new
                {
                    BrokerServer = brokerServer
                },
                cancellationToken: cancellationToken);
        }

        await _dbContext.ExecuteAsync(
            TopicPresetQueries.UpsertTopicPreset,
            sqlRequest,
            cancellationToken);

        var topicPreset = await _dbContext.QueryFirstOrDefaultAsync<TopicPresetSqlResponse>(
            TopicPresetQueries.GetTopicPresetByTopicPattern,
            new
            {
                BrokerServer = brokerServer,
                sqlRequest.TopicPattern
            },
            cancellationToken);

        if (topicPreset is null)
        {
            throw new InvalidOperationException($"Failed to read back topic preset '{sqlRequest.TopicPattern}' after upsert.");
        }

        return topicPreset.MapToTopicPreset();
    }
}
