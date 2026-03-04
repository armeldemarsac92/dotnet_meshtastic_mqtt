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

    public async Task<IReadOnlyCollection<TopicPreset>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Attempting to fetch topic presets");

        var topicPresets = await _dbContext.QueryAsync<TopicPresetSqlResponse>(
            TopicPresetQueries.GetTopicPresets,
            cancellationToken: cancellationToken);

        return topicPresets.Select(x => x.MapToTopicPreset()).ToList();
    }

    public async Task<TopicPreset> UpsertAsync(
        SaveTopicPresetRequest request,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Attempting to upsert topic preset: {TopicPattern}", request.TopicPattern);

        var sqlRequest = request.ToSqlRequest();

        if (sqlRequest.IsDefault == 1)
        {
            await _dbContext.ExecuteAsync(
                TopicPresetQueries.ClearDefaultTopicPresets,
                cancellationToken: cancellationToken);
        }

        await _dbContext.ExecuteAsync(
            TopicPresetQueries.UpsertTopicPreset,
            sqlRequest,
            cancellationToken);

        var topicPreset = await _dbContext.QueryFirstOrDefaultAsync<TopicPresetSqlResponse>(
            TopicPresetQueries.GetTopicPresetByTopicPattern,
            new { sqlRequest.TopicPattern },
            cancellationToken);

        if (topicPreset is null)
        {
            throw new InvalidOperationException($"Failed to read back topic preset '{sqlRequest.TopicPattern}' after upsert.");
        }

        return topicPreset.MapToTopicPreset();
    }
}
