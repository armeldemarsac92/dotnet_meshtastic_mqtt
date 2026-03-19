using MeshBoard.Application.Abstractions.Persistence;
using MeshBoard.Contracts.Topics;
using MeshBoard.Infrastructure.Persistence.Context;
using MeshBoard.Infrastructure.Persistence.SQL;
using MeshBoard.Infrastructure.Persistence.SQL.Responses;
using Microsoft.Extensions.Logging;
using System.Globalization;

namespace MeshBoard.Infrastructure.Persistence.Repositories;

internal sealed class SavedChannelFilterRepository : ISavedChannelFilterRepository
{
    private readonly IDbContext _dbContext;
    private readonly ILogger<SavedChannelFilterRepository> _logger;

    public SavedChannelFilterRepository(IDbContext dbContext, ILogger<SavedChannelFilterRepository> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<IReadOnlyCollection<SavedChannelFilter>> GetAllAsync(
        string workspaceId,
        Guid brokerServerProfileId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug(
            "Attempting to fetch saved channel filters for workspace {WorkspaceId} and profile {ProfileId}",
            workspaceId,
            brokerServerProfileId);

        var responses = await _dbContext.QueryAsync<SavedChannelFilterSqlResponse>(
            SavedChannelFilterQueries.GetAll,
            new
            {
                WorkspaceId = workspaceId,
                BrokerServerProfileId = brokerServerProfileId.ToString()
            },
            cancellationToken);

        return responses.Select(Map).ToList();
    }

    public async Task<bool> UpsertAsync(
        string workspaceId,
        Guid brokerServerProfileId,
        string topicFilter,
        string? label,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Attempting to upsert saved channel filter {TopicFilter} for workspace {WorkspaceId} and profile {ProfileId}",
            topicFilter,
            workspaceId,
            brokerServerProfileId);

        var affectedRows = await _dbContext.ExecuteAsync(
            SavedChannelFilterQueries.Upsert,
            new
            {
                Id = Guid.NewGuid().ToString(),
                WorkspaceId = workspaceId,
                BrokerServerProfileId = brokerServerProfileId.ToString(),
                TopicFilter = topicFilter,
                Label = label,
                CreatedAtUtc = DateTimeOffset.UtcNow.ToString("O"),
                UpdatedAtUtc = DateTimeOffset.UtcNow.ToString("O")
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
            "Attempting to delete saved channel filter {TopicFilter} for workspace {WorkspaceId} and profile {ProfileId}",
            topicFilter,
            workspaceId,
            brokerServerProfileId);

        var affectedRows = await _dbContext.ExecuteAsync(
            SavedChannelFilterQueries.Delete,
            new
            {
                WorkspaceId = workspaceId,
                BrokerServerProfileId = brokerServerProfileId.ToString(),
                TopicFilter = topicFilter
            },
            cancellationToken);

        return affectedRows > 0;
    }

    private static SavedChannelFilter Map(SavedChannelFilterSqlResponse response)
    {
        return new SavedChannelFilter
        {
            Id = Guid.Parse(response.Id),
            BrokerServerProfileId = Guid.Parse(response.BrokerServerProfileId),
            TopicFilter = response.TopicFilter,
            Label = response.Label,
            CreatedAtUtc = ParseOrDefault(response.CreatedAtUtc),
            UpdatedAtUtc = ParseOrDefault(response.UpdatedAtUtc)
        };
    }

    private static DateTimeOffset ParseOrDefault(string value)
    {
        return DateTimeOffset.TryParse(
            value,
            CultureInfo.InvariantCulture,
            DateTimeStyles.RoundtripKind,
            out var parsedValue)
            ? parsedValue
            : DateTimeOffset.UtcNow;
    }
}
