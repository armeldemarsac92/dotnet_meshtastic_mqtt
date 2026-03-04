using MeshBoard.Application.Abstractions.Persistence;
using MeshBoard.Contracts.Messages;
using Microsoft.Extensions.Logging;

namespace MeshBoard.Application.Services;

public interface IMessageService
{
    Task<IReadOnlyCollection<MessageSummary>> GetRecentMessages(int take = 250, CancellationToken cancellationToken = default);
}

public sealed class MessageService : IMessageService
{
    private readonly ILogger<MessageService> _logger;
    private readonly IMessageRepository _messageRepository;

    public MessageService(IMessageRepository messageRepository, ILogger<MessageService> logger)
    {
        _messageRepository = messageRepository;
        _logger = logger;
    }

    public async Task<IReadOnlyCollection<MessageSummary>> GetRecentMessages(
        int take = 250,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Attempting to get recent messages with take: {Take}", take);

        var messages = await _messageRepository.GetRecentAsync(take, cancellationToken);

        _logger.LogDebug("Retrieved {MessageCount} recent messages", messages.Count);

        return messages;
    }
}
