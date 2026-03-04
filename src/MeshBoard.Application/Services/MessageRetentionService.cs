using MeshBoard.Application.Abstractions.Persistence;
using MeshBoard.Contracts.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MeshBoard.Application.Services;

public interface IMessageRetentionService
{
    Task<int> PruneExpiredMessages(CancellationToken cancellationToken = default);
}

public sealed class MessageRetentionService : IMessageRetentionService
{
    private readonly ILogger<MessageRetentionService> _logger;
    private readonly IMessageRepository _messageRepository;
    private readonly PersistenceOptions _persistenceOptions;
    private readonly TimeProvider _timeProvider;

    public MessageRetentionService(
        IMessageRepository messageRepository,
        TimeProvider timeProvider,
        IOptions<PersistenceOptions> persistenceOptions,
        ILogger<MessageRetentionService> logger)
    {
        _messageRepository = messageRepository;
        _timeProvider = timeProvider;
        _persistenceOptions = persistenceOptions.Value;
        _logger = logger;
    }

    public async Task<int> PruneExpiredMessages(CancellationToken cancellationToken = default)
    {
        var cutoffUtc = _timeProvider.GetUtcNow().AddDays(-_persistenceOptions.MessageRetentionDays);

        _logger.LogInformation(
            "Attempting to prune message history with retention of {RetentionDays} days. Cutoff: {CutoffUtc}",
            _persistenceOptions.MessageRetentionDays,
            cutoffUtc);

        var deletedCount = await _messageRepository.DeleteOlderThanAsync(cutoffUtc, cancellationToken);

        _logger.LogInformation("Pruned {DeletedCount} expired messages", deletedCount);

        return deletedCount;
    }
}
