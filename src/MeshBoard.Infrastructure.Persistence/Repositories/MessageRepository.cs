using MeshBoard.Application.Abstractions.Persistence;
using MeshBoard.Contracts.Messages;
using MeshBoard.Infrastructure.Persistence.Context;
using MeshBoard.Infrastructure.Persistence.Mapping;
using MeshBoard.Infrastructure.Persistence.SQL;
using MeshBoard.Infrastructure.Persistence.SQL.Responses;
using Microsoft.Extensions.Logging;

namespace MeshBoard.Infrastructure.Persistence.Repositories;

internal sealed class MessageRepository : IMessageRepository
{
    private readonly IDbContext _dbContext;
    private readonly ILogger<MessageRepository> _logger;

    public MessageRepository(IDbContext dbContext, ILogger<MessageRepository> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<bool> AddAsync(SaveObservedMessageRequest request, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Attempting to add observed message from node: {NodeId}", request.FromNodeId);

        var rowsAffected = await _dbContext.ExecuteAsync(
            MessageQueries.InsertMessage,
            request.ToSqlRequest(),
            cancellationToken);

        return rowsAffected > 0;
    }

    public async Task<IReadOnlyCollection<MessageSummary>> GetRecentAsync(
        int take,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Attempting to fetch recent messages with take: {Take}", take);

        var sqlResponses = await _dbContext.QueryAsync<MessageSummarySqlResponse>(
            MessageQueries.GetRecentMessages,
            new { Take = take },
            cancellationToken);

        return sqlResponses.MapToMessages();
    }
}
