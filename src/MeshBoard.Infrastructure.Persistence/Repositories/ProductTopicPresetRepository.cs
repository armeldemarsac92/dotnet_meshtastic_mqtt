using MeshBoard.Application.Abstractions.Persistence;
using MeshBoard.Contracts.Topics;
using MeshBoard.Infrastructure.Persistence.Context;
using MeshBoard.Infrastructure.Persistence.Mapping;
using MeshBoard.Infrastructure.Persistence.SQL;
using MeshBoard.Infrastructure.Persistence.SQL.Responses;
using Microsoft.Extensions.Logging;

namespace MeshBoard.Infrastructure.Persistence.Repositories;

internal sealed class ProductTopicPresetRepository : ITopicPresetRepository
{
    private readonly IDbContext _dbContext;
    private readonly ILogger<ProductTopicPresetRepository> _logger;

    public ProductTopicPresetRepository(
        IDbContext dbContext,
        ILogger<ProductTopicPresetRepository> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<IReadOnlyCollection<TopicPreset>> GetAllAsync(
        string workspaceId,
        string brokerServer,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug(
            "Attempting to fetch product-path topic presets for workspace {WorkspaceId} and broker {BrokerServer}",
            workspaceId,
            brokerServer);

        var topicPresets = await _dbContext.QueryAsync<TopicPresetSqlResponse>(
            ProductTopicPresetQueries.GetTopicPresets,
            new
            {
                WorkspaceId = workspaceId,
                BrokerServer = brokerServer
            },
            cancellationToken: cancellationToken);

        return topicPresets.Select(x => x.MapToTopicPreset()).ToList();
    }

    public async Task<TopicPreset?> GetByTopicPatternAsync(
        string workspaceId,
        string brokerServer,
        string topicPattern,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug(
            "Attempting to fetch product-path topic preset {TopicPattern} for workspace {WorkspaceId} and broker {BrokerServer}",
            topicPattern,
            workspaceId,
            brokerServer);

        var response = await _dbContext.QueryFirstOrDefaultAsync<TopicPresetSqlResponse>(
            ProductTopicPresetQueries.GetTopicPresetByTopicPattern,
            new
            {
                WorkspaceId = workspaceId,
                BrokerServer = brokerServer,
                TopicPattern = topicPattern
            },
            cancellationToken);

        return response?.MapToTopicPreset();
    }

    public async Task<TopicPreset> UpsertAsync(
        string workspaceId,
        string brokerServer,
        SaveTopicPresetRequest request,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Attempting to upsert product-path topic preset: {TopicPattern} for workspace {WorkspaceId} and broker {BrokerServer}",
            request.TopicPattern,
            workspaceId,
            brokerServer);

        var sqlRequest = request.ToSqlRequest(workspaceId, brokerServer);

        if (sqlRequest.IsDefault == 1)
        {
            await _dbContext.ExecuteAsync(
                ProductTopicPresetQueries.ClearDefaultTopicPresets,
                new
                {
                    WorkspaceId = workspaceId,
                    BrokerServer = brokerServer
                },
                cancellationToken: cancellationToken);
        }

        await _dbContext.ExecuteAsync(
            ProductTopicPresetQueries.UpsertTopicPreset,
            sqlRequest,
            cancellationToken);

        var topicPreset = await _dbContext.QueryFirstOrDefaultAsync<TopicPresetSqlResponse>(
            ProductTopicPresetQueries.GetTopicPresetByTopicPattern,
            new
            {
                WorkspaceId = workspaceId,
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
